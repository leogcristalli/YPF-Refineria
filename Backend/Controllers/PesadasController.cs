using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Backend.Auth;
using Backend.Data;
using Backend.Hardware;
using Backend.Models;
using Microsoft.Extensions.Logging;

namespace Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Permiso(PuntoMenu.Pesadas)]
    public class PesadasController : ControllerBase
    {
        // Valores de los datos de sistema (t_TipoOrden, t_TipoPesada, t_EstadoOrdenTransporte).
        private const byte TipoOrdenDescarga = 2;   // 1 = Orden de Entrega, 2 = Autorización de Descarga
        private const byte PesadaTara = 1;
        private const byte PesadaBruto = 2;
        private const byte OtEnEjecucion = 3;
        private const byte OtEjecutado = 5;

        private const int LargoMaximoMotivo = 300;   // t_Pesada.MotivoIngresoManual_Pesada
        private static readonly int[] SqlClaveDuplicada = { 2601, 2627 };

        private readonly AppDbContext _context;
        private readonly RegistroLecturas _lecturas;
        private readonly ILogger<PesadasController> _logger;

        public PesadasController(AppDbContext context, RegistroLecturas lecturas, ILogger<PesadasController> logger)
        {
            _context = context;
            _lecturas = lecturas;
            _logger = logger;
        }

        // GET: api/Pesadas
        // Incluye el nombre de la báscula (aunque esté dada de baja); null = ingreso manual sin báscula.
        // Abierto a cualquier usuario logueado: lo usa el Panel de Control.
        [HttpGet]
        [Permiso]
        public async Task<ActionResult<IEnumerable<object>>> GetPesadas()
        {
            var pesadas = await _context.Pesadas
                .AsNoTracking()
                .Select(p => new
                {
                    p.Id_Pesada,
                    p.Id_Planificacion,
                    p.Id_TipoPesada,
                    p.Fecha,
                    p.Cantidad,
                    p.EsIngresoManual,
                    p.MotivoIngresoManual,
                    p.Id_Bascula,
                    BasculaDescripcion = _context.Basculas
                        .Where(b => b.Id_Bascula == p.Id_Bascula)
                        .Select(b => b.Descripcion)
                        .FirstOrDefault()
                })
                .ToListAsync();

            return Ok(pesadas);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Pesada>> GetPesada(long id)
        {
            var pesada = await _context.Pesadas.FindAsync(id);
            if (pesada == null)
            {
                return NotFound();
            }
            return pesada;
        }

        // POST: api/Pesadas
        // CU78: Registrar Pesada de Transporte
        //  - Entrega (despacho): el camión entra vacío (1.ª pesada = TARA) y sale cargado (2.ª = BRUTO).
        //  - Descarga (recepción): el camión entra cargado (1.ª pesada = BRUTO) y sale vacío (2.ª = TARA).
        //  - Automática: el peso tiene que ser exactamente la última lectura de esa báscula para esa orden.
        //    Si se tipeó o se corrigió a mano, es un ingreso manual y el motivo es obligatorio.
        // Al completarse las dos pesadas se calcula el neto, se valida la tolerancia del producto
        // y se cierra la orden. Si algo no cumple, no se guarda nada.
        [HttpPost]
        public async Task<ActionResult<object>> PostPesada(Pesada pesada)
        {
            if (pesada.Cantidad <= 0)
            {
                return BadRequest("El peso debe ser mayor a cero.");
            }

            // 1. Ingreso manual: el motivo es obligatorio
            var motivo = pesada.MotivoIngresoManual?.Trim();
            if (pesada.EsIngresoManual)
            {
                if (string.IsNullOrEmpty(motivo))
                    return BadRequest("El ingreso manual requiere indicar el motivo.");
                if (motivo.Length > LargoMaximoMotivo)
                    return BadRequest($"El motivo del ingreso manual admite hasta {LargoMaximoMotivo} caracteres.");
            }
            else
            {
                motivo = null;
                if (pesada.Id_Bascula == null)
                    return BadRequest("Una pesada automática requiere la báscula que leyó el peso.");
            }

            // 2. Báscula: debe existir, estar activa y tener el certificado vigente
            //    (una pesada manual puede no tener báscula).
            if (pesada.Id_Bascula != null)
            {
                var bascula = await _context.Basculas.AsNoTracking()
                    .FirstOrDefaultAsync(b => b.Id_Bascula == pesada.Id_Bascula);
                if (bascula == null)
                    return BadRequest("La báscula seleccionada no existe.");
                if (!bascula.Activo)
                    return BadRequest($"La báscula \"{bascula.Descripcion}\" está dada de baja.");
                if (!bascula.CertificadoVigente(DateTime.Today))
                    return BadRequest($"La báscula \"{bascula.Descripcion}\" tiene el certificado vencido " +
                                      $"({bascula.VencimientoCertificado:dd/MM/yyyy}). Use otra báscula o el ingreso manual sin báscula.");
            }

            // 3. Automática: el peso tiene que coincidir con la lectura de la báscula
            if (!pesada.EsIngresoManual)
            {
                var lectura = _lecturas.Obtener(pesada.Id_Bascula!.Value, pesada.Id_Planificacion);
                if (lectura == null || lectura.PesoKg != pesada.Cantidad)
                {
                    return BadRequest("El peso no coincide con una lectura reciente de la báscula. " +
                                      "Si lo ingresó o lo corrigió a mano, márquelo como ingreso manual e indique el motivo.");
                }
            }

            // 4. Validar que la Planificacion (Orden de Transporte) exista
            var planificacion = await _context.Planificaciones
                .Include(pl => pl.OrdenTransporte)
                    .ThenInclude(ot => ot!.OrdenDetalle)
                        .ThenInclude(od => od!.Orden)
                .Include(pl => pl.OrdenTransporte)
                    .ThenInclude(ot => ot!.OrdenDetalle)
                        .ThenInclude(od => od!.Producto)
                .FirstOrDefaultAsync(pl => pl.Id_Planificacion == pesada.Id_Planificacion);

            if (planificacion == null)
            {
                return BadRequest("La Planificación (Orden de Transporte) no existe.");
            }

            // 5. Validar que la Orden esté abierta
            var ot = planificacion.OrdenTransporte;
            var orden = ot?.OrdenDetalle?.Orden;
            var producto = ot?.OrdenDetalle?.Producto;
            if (ot == null || orden == null || !orden.Activo || !ot.Activo)
            {
                return BadRequest("La orden no está abierta (ya fue cerrada o cancelada).");
            }

            // 6. Validar pesajes pendientes
            var pesadasPrevias = await _context.Pesadas
                .AsNoTracking()
                .Where(p => p.Id_Planificacion == pesada.Id_Planificacion)
                .ToListAsync();

            if (pesadasPrevias.Count >= 2)
            {
                return BadRequest("La orden ya tiene sus pesadas completas (TARA y BRUTO).");
            }

            // 7. Determinar el tipo de pesada según el tipo de orden
            byte tipo;
            if (pesadasPrevias.Count == 0)
            {
                tipo = orden.Id_TipoOrden == TipoOrdenDescarga ? PesadaBruto : PesadaTara;
            }
            else
            {
                tipo = pesadasPrevias[0].Id_TipoPesada == PesadaTara ? PesadaBruto : PesadaTara;
            }

            // 8. Si es la segunda pesada: calcular neto y validar (antes de guardar nada)
            decimal? pesoNeto = null;
            if (pesadasPrevias.Count == 1)
            {
                var previa = pesadasPrevias[0];
                var pesoTara = tipo == PesadaTara ? pesada.Cantidad : previa.Cantidad;
                var pesoBruto = tipo == PesadaBruto ? pesada.Cantidad : previa.Cantidad;
                pesoNeto = pesoBruto - pesoTara;

                if (pesoNeto <= 0)
                {
                    return BadRequest($"El peso Bruto ({pesoBruto:N2} kg) debe ser mayor que la Tara ({pesoTara:N2} kg).");
                }

                // Regla de tolerancia: el neto debe estar dentro del margen del producto
                // respecto de la cantidad planificada.
                if (producto != null && planificacion.CantidadKG > 0)
                {
                    decimal diferenciaPct = Math.Abs((pesoNeto.Value - planificacion.CantidadKG) / planificacion.CantidadKG * 100m);
                    if (diferenciaPct > producto.ToleranciaPorc)
                    {
                        return BadRequest(
                            $"Peso fuera de tolerancia: neto {pesoNeto.Value:N2} kg contra {planificacion.CantidadKG:N2} kg planificados " +
                            $"(diferencia {diferenciaPct:N2} %, tolerancia del producto {producto.ToleranciaPorc:N2} %). La pesada no fue registrada.");
                    }
                }
            }

            // 9. Registrar la pesada y actualizar el estado de la orden en una única operación
            var ahora = DateTime.Now;
            pesada.Id_Pesada = 0;
            pesada.Id_TipoPesada = tipo;
            pesada.Fecha = ahora;
            pesada.MotivoIngresoManual = motivo;
            _context.Pesadas.Add(pesada);

            if (pesadasPrevias.Count == 0)
            {
                ot.Id_EstadoOrdenTransporte = OtEnEjecucion;
                ot.FechaInicioEjecucion ??= ahora;
            }
            else
            {
                ot.Id_EstadoOrdenTransporte = OtEjecutado;
                ot.FechaFinEjecucion = ahora;
                orden.Activo = false;
                orden.FechaCierre = ahora;
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && SqlClaveDuplicada.Contains(sql.Number))
            {
                // Índice único (Id_Planificacion, Id_TipoPesada): otra estación la grabó al mismo tiempo.
                return Conflict("Esa pesada ya fue registrada para la orden (posiblemente desde otra estación). Actualice la pantalla.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar la pesada de la planificación {Id}", pesada.Id_Planificacion);
                return StatusCode(500, "No se pudo registrar la pesada. Intente nuevamente.");
            }

            if (!pesada.EsIngresoManual)
            {
                _lecturas.Consumir(pesada.Id_Bascula!.Value, pesada.Id_Planificacion);
            }

            return CreatedAtAction("GetPesada", new { id = pesada.Id_Pesada }, new
            {
                pesada.Id_Pesada,
                pesada.Id_Planificacion,
                pesada.Id_TipoPesada,
                pesada.Fecha,
                pesada.Cantidad,
                pesada.EsIngresoManual,
                pesada.MotivoIngresoManual,
                pesada.Id_Bascula,
                pesoNeto
            });
        }

        [HttpGet("ByPlanificacion/{idPlanificacion}")]
        public async Task<ActionResult<IEnumerable<Pesada>>> GetPesadasByPlanificacion(long idPlanificacion)
        {
            return await _context.Pesadas.Where(p => p.Id_Planificacion == idPlanificacion).ToListAsync();
        }

        // También lo usa el detalle de una orden en Órdenes de Transporte y Planificación.
        [HttpGet("ByOrdenTransporte/{idOrdenTransporte}")]
        [Permiso(PuntoMenu.Pesadas, PuntoMenu.OrdenesTransporte, PuntoMenu.AutorizacionesDescarga, PuntoMenu.OrdenesEntrega)]
        public async Task<ActionResult<IEnumerable<Pesada>>> GetPesadasByOrdenTransporte(long idOrdenTransporte)
        {
            // Join via Planificacion
            return await _context.Pesadas
                .Include(p => p.Planificacion)
                .Where(p => p.Planificacion!.Id_OrdenTransporte == idOrdenTransporte)
                .ToListAsync();
        }

        // GET: api/Pesadas/Ticket/{idPlanificacion}
        // CU79: Emitir Ticket de Pesada
        [HttpGet("Ticket/{idPlanificacion}")]
        public async Task<ActionResult<object>> GetTicket(long idPlanificacion)
        {
            var planificacion = await _context.Planificaciones
                .Include(pl => pl.OrdenTransporte)
                    .ThenInclude(ot => ot!.OrdenDetalle)
                        .ThenInclude(od => od!.Producto)
                .Include(pl => pl.OrdenTransporte)
                    .ThenInclude(ot => ot!.OrdenDetalle)
                        .ThenInclude(od => od!.Orden)
                .Include(pl => pl.OrdenTransporte)
                    .ThenInclude(ot => ot!.Vehiculo)
                .Include(pl => pl.OrdenTransporte)
                    .ThenInclude(ot => ot!.Conductor)
                .Include(pl => pl.OrdenTransporte)
                    .ThenInclude(ot => ot!.RazonSocialTransportista)
                .FirstOrDefaultAsync(pl => pl.Id_Planificacion == idPlanificacion);

            if (planificacion == null)
                return NotFound("Planificación no encontrada.");

            var pesadas = await _context.Pesadas
                .Where(p => p.Id_Planificacion == idPlanificacion)
                .OrderBy(p => p.Id_TipoPesada)
                .ToListAsync();

            if (pesadas.Count < 2)
                return BadRequest("La planificación aún no tiene las dos pesadas completas (TARA y BRUTO).");

            var tara = pesadas.FirstOrDefault(p => p.Id_TipoPesada == PesadaTara);
            var bruto = pesadas.FirstOrDefault(p => p.Id_TipoPesada == PesadaBruto);
            decimal pesoNeto = (bruto?.Cantidad ?? 0) - (tara?.Cantidad ?? 0);

            // Nombre de la báscula de cada pesada (null = ingreso manual sin báscula).
            var idsBascula = pesadas.Where(p => p.Id_Bascula != null).Select(p => p.Id_Bascula!.Value).Distinct().ToList();
            var basculas = await _context.Basculas.AsNoTracking()
                .Where(b => idsBascula.Contains(b.Id_Bascula))
                .ToDictionaryAsync(b => b.Id_Bascula, b => b.Descripcion);
            string? NombreBascula(Pesada p) =>
                p.Id_Bascula != null && basculas.TryGetValue(p.Id_Bascula.Value, out var nombre) ? nombre : null;

            var ot = planificacion.OrdenTransporte;
            var producto = ot?.OrdenDetalle?.Producto;
            bool dentroTolerancia = true;
            if (producto != null && planificacion.CantidadKG > 0)
            {
                decimal diferenciaPct = Math.Abs((pesoNeto - planificacion.CantidadKG) / planificacion.CantidadKG * 100m);
                dentroTolerancia = diferenciaPct <= producto.ToleranciaPorc;
            }

            var conductor = ot?.Conductor;
            string conductorNombre = conductor != null
                ? $"{conductor.Apellido}, {conductor.Nombre}"
                : "N/A";

            object? Detalle(Pesada? p) => p == null ? null : new
            {
                fecha = p.Fecha,
                cantidad = p.Cantidad,
                bascula = p.Id_Bascula,
                basculaDescripcion = NombreBascula(p),
                esIngresoManual = p.EsIngresoManual,
                motivoIngresoManual = p.MotivoIngresoManual
            };

            return Ok(new
            {
                idPlanificacion = planificacion.Id_Planificacion,
                idOrdenTransporte = ot?.Id_OrdenTransporte,
                nroRemito = ot?.OrdenDetalle?.NroRemito,
                producto = producto?.Descripcion ?? "N/A",
                vehiculo = ot?.Vehiculo?.PatenteChasis ?? "N/A",
                conductor = conductorNombre,
                transportista = ot?.RazonSocialTransportista?.Descripcion ?? "N/A",
                pesadaTara = Detalle(tara),
                pesadaBruto = Detalle(bruto),
                pesoNeto,
                cantidadPlanificada = planificacion.CantidadKG,
                dentroTolerancia,
                fechaEmision = DateTime.Now
            });
        }
    }
}
