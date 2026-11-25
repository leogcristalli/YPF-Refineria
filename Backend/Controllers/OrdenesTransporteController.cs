using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend.Auth;
using Backend.Data;
using Backend.Models;

namespace Backend.Controllers
{
    // Autorizaciones de Descarga (CU060-CU065), Órdenes de Entrega (CU066-CU071) y Órdenes de
    // Transporte (CU072-CU076). Cada alta crea orden + detalle + OT + planificación.
    //
    // Ciclo de estados:
    //   alta          -> orden Planificado, OT Planificado
    //   replanificar  -> OT Replanificado (nueva fecha + motivo)
    //   1.ª pesada    -> OT Ejecución                     (PesadasController)
    //   2.ª pesada    -> orden Definitivo (cerrada), OT Ejecutado (PesadasController)
    //   cancelar      -> orden Anulado (fecha y motivo de anulación)
    [Route("api/[controller]")]
    [ApiController]
    [Permiso(PuntoMenu.OrdenesTransporte, PuntoMenu.AutorizacionesDescarga, PuntoMenu.OrdenesEntrega)]
    public class OrdenesTransporteController : ControllerBase
    {
        private readonly AppDbContext _context;

        // Ids fijos cargados por los scripts sysdata del proyecto Database.
        private const byte EstadoOrdenPlanificado = 4;     // t_EstadoOrden
        private const byte EstadoOrdenAnulado = 5;
        private const byte EstadoOtPlanificado = 2;        // t_EstadoOrdenTransporte
        private const byte EstadoOtEnEjecucion = 3;
        private const byte EstadoOtReplanificado = 4;
        private const byte TipoOrdenEntrega = 1;           // t_TipoOrden: 1 = Orden de Entrega, 2 = Autorización de Descarga
        private const byte TipoOrdenDescarga = 2;
        // La cantidad se guarda en DECIMAL(10,2): ese es el máximo que admite la base de datos.
        private const decimal CantidadMaximaKg = 99_999_999.99m;
        private const byte UnidadKilos = 1;                // t_UnidadMedida
        private const byte UnidadLitros = 3;

        private const int LargoMaximoMotivoAnulacion = 5000;     // MotivoAnulacion_Orden
        private const int LargoMaximoMotivoReplanificacion = 500;

        public OrdenesTransporteController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<OrdenTransporte>>> Get()
        {
            return await _context.OrdenTransportes
                .Include(ot => ot.OrdenDetalle).ThenInclude(od => od!.Producto)
                .Include(ot => ot.OrdenDetalle).ThenInclude(od => od!.Orden).ThenInclude(o => o!.EstadoOrden)
                .Include(ot => ot.OrdenDetalle).ThenInclude(od => od!.Orden).ThenInclude(o => o!.Puesto)
                .Include(ot => ot.OrdenDetalle).ThenInclude(od => od!.TanquesAlmacenamiento)
                .Include(ot => ot.EstadoOrdenTransporte)
                .Include(ot => ot.Vehiculo)
                .Include(ot => ot.Conductor)
                .Include(ot => ot.RazonSocialTransportista)
                .ToListAsync();
        }

        // CU077: OT asignada al vehículo que está en la báscula.
        // Solo OT abiertas con pesadas pendientes. Si el camión ya hizo su primera pesada,
        // esa OT va primero (está en medio del circuito); después, la de fecha planificada más antigua.
        [HttpGet("ByPatente/{patente}")]
        [Permiso(PuntoMenu.Pesadas)]
        public async Task<ActionResult<OrdenTransporte>> GetByPatente(string patente)
        {
            var buscada = patente.Trim();
            var pendientes = _context.PendientesDePesaje();

            var item = await _context.OrdenTransportes
                .Include(ot => ot.OrdenDetalle).ThenInclude(od => od!.Producto)
                .Include(ot => ot.OrdenDetalle).ThenInclude(od => od!.Orden)
                .Include(ot => ot.Vehiculo)
                .Include(ot => ot.Conductor)
                .Include(ot => ot.RazonSocialTransportista)
                .Where(ot => ot.Vehiculo!.PatenteChasis == buscada
                    && pendientes.Any(p => p.Id_OrdenTransporte == ot.Id_OrdenTransporte))
                .OrderByDescending(ot => ot.Id_EstadoOrdenTransporte == EstadoOtEnEjecucion)
                .ThenBy(ot => ot.FechaPlanificacion)
                .ThenBy(ot => ot.Id_OrdenTransporte)
                .FirstOrDefaultAsync();

            if (item == null)
                return NotFound($"El vehículo {buscada.ToUpperInvariant()} no tiene órdenes de transporte pendientes de pesaje.");

            return item;
        }

        // Incluye los datos que muestra la pantalla de pesadas (producto, vehículo, conductor, transportista).
        [HttpGet("{id}")]
        [Permiso(PuntoMenu.Pesadas, PuntoMenu.OrdenesTransporte, PuntoMenu.AutorizacionesDescarga, PuntoMenu.OrdenesEntrega)]
        public async Task<ActionResult<OrdenTransporte>> Get(long id)
        {
            var item = await _context.OrdenTransportes
                .Include(ot => ot.OrdenDetalle).ThenInclude(od => od!.Producto)
                .Include(ot => ot.OrdenDetalle).ThenInclude(od => od!.Orden)
                .Include(ot => ot.Vehiculo)
                .Include(ot => ot.Conductor)
                .Include(ot => ot.RazonSocialTransportista)
                .FirstOrDefaultAsync(ot => ot.Id_OrdenTransporte == id);
            if (item == null) return NotFound();
            return item;
        }

        // ─── Permisos por tipo de orden ─────────────────────────────────────────

        // Órdenes de Transporte opera los dos tipos; Autorizaciones de Descarga y Órdenes de
        // Entrega, solo el suyo (pestañas de Planificación Logística).
        private bool PuedeOperar(byte tipoOrden) =>
            User.TienePunto(PuntoMenu.OrdenesTransporte)
            || User.TienePunto(tipoOrden == TipoOrdenDescarga ? PuntoMenu.AutorizacionesDescarga : PuntoMenu.OrdenesEntrega);

        private static ObjectResult SinPermisoTipo(byte tipoOrden) =>
            Permisos.Prohibido(tipoOrden == TipoOrdenDescarga
                ? "No tiene permiso para operar autorizaciones de descarga."
                : "No tiene permiso para operar órdenes de entrega.");

        // ─── Validaciones compartidas por el alta y la modificación ─────────────

        // Devuelve un mensaje de error o null si los datos son válidos.
        private async Task<string?> ValidarAsync(OrdenTransporteCreationDto dto, byte tipoOrden, long? idOrdenDetalleActual = null)
        {
            var esDescarga = tipoOrden == TipoOrdenDescarga;

            // Sin los dos controles siguientes, 0,004 pasaría el "mayor a cero" pero la base lo
            // redondea a 0,00, y un valor enorme provocaría un error de desbordamiento en SQL Server.
            if (dto.Cantidad <= 0)
                return "La cantidad debe ser mayor a cero.";
            if (dto.Cantidad != decimal.Round(dto.Cantidad, 2))
                return "La cantidad admite como máximo dos decimales.";
            if (dto.Cantidad > CantidadMaximaKg)
                return $"La cantidad no puede superar {CantidadMaximaKg:N2} kg.";
            if (dto.NroRemito <= 0)
                return "El número de remito debe ser mayor a cero.";
            if (await _context.OrdenDetalles.AnyAsync(d =>
                d.Activo && d.Orden!.Activo && d.NroRemito == dto.NroRemito && d.Id_OrdenDetalle != idOrdenDetalleActual))
                return $"Ya existe una orden activa con el número de remito {dto.NroRemito}.";

            if (!await _context.Puestos.AnyAsync(p => p.Id_PuestoExpedicionRecepcion == dto.Id_PuestoExpedicionRecepcion && p.Activo))
                return esDescarga
                    ? "Seleccione un centro de recepción existente y activo."
                    : "Seleccione un puesto de expedición existente y activo.";

            if (!await _context.Productos.AnyAsync(p => p.Id_Producto == dto.Id_Producto && p.Activo))
                return "El producto no existe o está dado de baja.";
            if (!await _context.RazonesSociales.AnyAsync(r => r.Id_RazonSocial == dto.Id_RazonSocialCliente && r.Activo))
                return esDescarga ? "El proveedor no existe o está dado de baja." : "El cliente no existe o está dado de baja.";
            if (!await _context.RazonesSociales.AnyAsync(r => r.Id_RazonSocial == dto.Id_RazonSocialTransportista && r.Activo))
                return "El transportista no existe o está dado de baja.";
            if (!await _context.Vehiculos.AnyAsync(v => v.Id_Vehiculo == dto.Id_Vehiculo && v.Activo))
                return "El vehículo no existe o está dado de baja.";
            if (!await _context.Conductores.AnyAsync(c => c.Id_Conductor == dto.Id_Conductor && c.Activo))
                return "El conductor no existe o está dado de baja.";

            // CU061: la autorización de descarga define el tanque de destino.
            if (esDescarga && (dto.Id_Tanque ?? 0) <= 0)
                return "Una autorización de descarga requiere el tanque de destino.";

            return null;
        }

        // Vincula el tanque a la orden (destino en una descarga, origen opcional en una entrega).
        // La capacidad del tanque está en litros y la cantidad en kilos: se convierte con la
        // densidad del producto. Una descarga reserva el volumen y valida la capacidad disponible.
        private async Task<string?> VincularTanqueAsync(long idDetalle, int? idTanque, int idProducto, decimal cantidadKg, bool esDescarga)
        {
            if (idTanque is null or <= 0) return null;

            var tanque = await _context.Tanques.FindAsync(idTanque.Value);
            if (tanque == null || !tanque.Activo)
                return "El tanque seleccionado no existe o no está activo.";

            var producto = await _context.Productos.FindAsync(idProducto);
            if (producto == null || producto.Densidad <= 0)
                return "El producto no tiene una densidad válida para calcular el volumen.";

            var litros = Math.Round(cantidadKg / producto.Densidad, 2);
            if (esDescarga)
            {
                var disponible = tanque.VolumenMaximo - tanque.VolumenOcupado;
                if (litros > disponible)
                    return $"Capacidad insuficiente en el tanque {tanque.Codigo}: disponible {disponible:N2} L, requerido {litros:N2} L ({cantidadKg:N2} kg).";
                tanque.VolumenOcupado += litros;
            }

            _context.TanqueAlmacenamientos.Add(new TanqueAlmacenamiento
            {
                Id_OrdenDetalle = idDetalle,
                Id_Tanque = idTanque.Value,
                Volumen = litros,
                Activo = true
            });
            return null;
        }

        // Libera el volumen reservado en el tanque por una descarga abierta (al modificarla,
        // cancelarla o eliminarla). No hace nada si la orden ya está cerrada o anulada.
        private async Task LiberarTanqueAsync(OrdenDetalle? detalle)
        {
            if (detalle?.Orden == null || !detalle.Orden.Activo || detalle.Orden.Id_TipoOrden != TipoOrdenDescarga)
                return;

            var vinculos = await _context.TanqueAlmacenamientos
                .Where(ta => ta.Id_OrdenDetalle == detalle.Id_OrdenDetalle)
                .ToListAsync();

            foreach (var vinculo in vinculos)
            {
                var tanque = await _context.Tanques.FindAsync(vinculo.Id_Tanque);
                if (tanque != null)
                {
                    tanque.VolumenOcupado = Math.Max(0, tanque.VolumenOcupado - vinculo.Volumen);
                }
            }
        }

        // La orden se puede modificar o replanificar: abierta (ni cerrada ni anulada) y sin pesadas.
        private async Task<string?> ValidarOrdenEditableAsync(OrdenTransporte transporte, Orden orden, string accion)
        {
            if (orden.FechaCierre != null)
                return $"La orden ya fue cerrada con sus pesadas y no puede {accion}.";
            if (!orden.Activo || !transporte.Activo)
                return $"La orden está anulada y no puede {accion}.";
            if (await _context.Planificaciones.AnyAsync(p => p.Id_OrdenTransporte == transporte.Id_OrdenTransporte
                    && _context.Pesadas.Any(x => x.Id_Planificacion == p.Id_Planificacion)))
                return $"La orden ya tiene pesadas registradas (en ejecución) y no puede {accion}.";
            return null;
        }

        private async Task<OrdenTransporte?> CargarAsync(long id) =>
            await _context.OrdenTransportes
                .Include(t => t.OrdenDetalle)
                .ThenInclude(d => d!.Orden)
                .FirstOrDefaultAsync(t => t.Id_OrdenTransporte == id);

        // ─── Alta (CU061 / CU067 / CU073) ───────────────────────────────────────

        [HttpPost]
        public async Task<ActionResult<OrdenTransporte>> Post(OrdenTransporteCreationDto dto)
        {
            var tipoOrden = dto.Id_TipoOrden == TipoOrdenDescarga ? TipoOrdenDescarga : TipoOrdenEntrega;
            if (!PuedeOperar(tipoOrden)) return SinPermisoTipo(tipoOrden);

            var error = await ValidarAsync(dto, tipoOrden);
            if (error != null) return BadRequest(error);

            using var transaction = await _context.Database.BeginTransactionAsync();
            var ahora = DateTime.Now;

            // 1. Orden: nace Planificada (el alta ya incluye el transporte asignado).
            var orden = new Orden
            {
                Id_EstadoOrden = EstadoOrdenPlanificado,
                Id_TipoOrden = tipoOrden,
                Id_PuestoExpedicionRecepcion = dto.Id_PuestoExpedicionRecepcion,
                FechaPlanificacion = dto.Fecha
            };
            _context.Ordenes.Add(orden);
            await _context.SaveChangesAsync();

            // 2. Detalle
            var detalle = new OrdenDetalle
            {
                Id_Orden = orden.Id_Orden,
                Id_Producto = dto.Id_Producto,
                Id_RazonSocialClienteProveedor = dto.Id_RazonSocialCliente,
                NroRemito = dto.NroRemito,
                Peso = dto.Cantidad,
                Id_UnidadMedidaPeso = UnidadKilos,
                Volumen = 0,
                Id_UnidadMedidaVolumen = UnidadLitros,
                EsMovimientoInterno = false
            };
            _context.OrdenDetalles.Add(detalle);
            await _context.SaveChangesAsync();

            // 3. Orden de transporte, ya planificada
            var transporte = new OrdenTransporte
            {
                Id_OrdenDetalle = detalle.Id_OrdenDetalle,
                Id_Vehiculo = dto.Id_Vehiculo,
                Id_Conductor = dto.Id_Conductor,
                Id_RazonSocialTransportista = dto.Id_RazonSocialTransportista,
                Id_EstadoOrdenTransporte = EstadoOtPlanificado,
                FechaPlanificacion = dto.Fecha,
                FechaFinPlanificacion = ahora
            };
            _context.OrdenTransportes.Add(transporte);
            await _context.SaveChangesAsync();

            // 4. Planificación
            _context.Planificaciones.Add(new Planificacion
            {
                Id_OrdenTransporte = transporte.Id_OrdenTransporte,
                NroPosicion = 1,
                CantidadPlanificada = dto.Cantidad,
                Id_UnidadMedidaCantidadPlanificada = UnidadKilos,
                CantidadKG = dto.Cantidad
            });

            // 5. Tanque (CU061)
            error = await VincularTanqueAsync(detalle.Id_OrdenDetalle, dto.Id_Tanque, dto.Id_Producto, dto.Cantidad, tipoOrden == TipoOrdenDescarga);
            if (error != null)
            {
                await transaction.RollbackAsync();
                return BadRequest(error);
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return CreatedAtAction(nameof(Get), new { id = transporte.Id_OrdenTransporte }, transporte);
        }

        // ─── Modificar (CU063 / CU069 / CU075) ──────────────────────────────────
        // Solo órdenes abiertas sin pesadas. La fecha no se cambia acá: se cambia con Replanificar.
        [HttpPut("{id}")]
        public async Task<IActionResult> Put(long id, OrdenTransporteCreationDto dto)
        {
            var transporte = await CargarAsync(id);
            var orden = transporte?.OrdenDetalle?.Orden;
            if (transporte == null || orden == null) return NotFound();

            var tipoOrden = orden.Id_TipoOrden;
            if (!PuedeOperar(tipoOrden)) return SinPermisoTipo(tipoOrden);

            var error = await ValidarOrdenEditableAsync(transporte, orden, "modificarse")
                ?? await ValidarAsync(dto, tipoOrden, transporte.Id_OrdenDetalle);
            if (error != null) return BadRequest(error);

            using var transaction = await _context.Database.BeginTransactionAsync();
            var detalle = transporte.OrdenDetalle!;

            // Tanque: se libera la reserva anterior y se vuelve a vincular con los datos nuevos,
            // así la capacidad se valida contando el volumen liberado.
            await LiberarTanqueAsync(detalle);
            _context.TanqueAlmacenamientos.RemoveRange(
                await _context.TanqueAlmacenamientos.Where(ta => ta.Id_OrdenDetalle == detalle.Id_OrdenDetalle).ToListAsync());

            orden.Id_PuestoExpedicionRecepcion = dto.Id_PuestoExpedicionRecepcion;

            detalle.Id_Producto = dto.Id_Producto;
            detalle.Id_RazonSocialClienteProveedor = dto.Id_RazonSocialCliente;
            detalle.NroRemito = dto.NroRemito;
            detalle.Peso = dto.Cantidad;

            transporte.Id_Vehiculo = dto.Id_Vehiculo;
            transporte.Id_Conductor = dto.Id_Conductor;
            transporte.Id_RazonSocialTransportista = dto.Id_RazonSocialTransportista;

            var plan = await _context.Planificaciones.FirstOrDefaultAsync(p => p.Id_OrdenTransporte == id);
            if (plan != null)
            {
                plan.CantidadPlanificada = dto.Cantidad;
                plan.CantidadKG = dto.Cantidad;
            }

            error = await VincularTanqueAsync(detalle.Id_OrdenDetalle, dto.Id_Tanque, dto.Id_Producto, dto.Cantidad, tipoOrden == TipoOrdenDescarga);
            if (error != null)
            {
                await transaction.RollbackAsync();
                return BadRequest(error);
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return NoContent();
        }

        // ─── Replanificar (CU075) ───────────────────────────────────────────────
        // Cambia la fecha planificada con un motivo obligatorio. La OT queda Replanificada y se
        // registra cuándo y por qué (lo usa el filtro "Replanificado" del reporte CU080).
        [HttpPatch("{id}/replanificar")]
        public async Task<IActionResult> Replanificar(long id, [FromBody] ReplanificarOrdenDto dto)
        {
            var transporte = await CargarAsync(id);
            var orden = transporte?.OrdenDetalle?.Orden;
            if (transporte == null || orden == null) return NotFound();

            if (!PuedeOperar(orden.Id_TipoOrden)) return SinPermisoTipo(orden.Id_TipoOrden);

            var error = await ValidarOrdenEditableAsync(transporte, orden, "replanificarse");
            if (error != null) return BadRequest(error);

            var motivo = dto.Motivo?.Trim() ?? "";
            if (motivo.Length == 0)
                return BadRequest("Indique el motivo de la replanificación.");
            if (motivo.Length > LargoMaximoMotivoReplanificacion)
                return BadRequest($"El motivo de la replanificación admite hasta {LargoMaximoMotivoReplanificacion} caracteres.");

            if (dto.Fecha == null)
                return BadRequest("Indique la nueva fecha planificada.");
            var fecha = dto.Fecha.Value;
            if (fecha.Date < DateTime.Today)
                return BadRequest("La nueva fecha no puede ser anterior a hoy.");
            if (fecha.Date == transporte.FechaPlanificacion.Date)
                return BadRequest("La nueva fecha debe ser distinta de la fecha planificada actual.");

            transporte.FechaPlanificacion = fecha;
            orden.FechaPlanificacion = fecha;
            transporte.Id_EstadoOrdenTransporte = EstadoOtReplanificado;
            transporte.FechaFinReplanificacion = DateTime.Now;
            transporte.MotivoReplanificacion = motivo;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Orden replanificada." });
        }

        // ─── Cancelar (CU064 / CU070) ───────────────────────────────────────────
        [HttpPatch("{id}/cancelar")]
        public async Task<IActionResult> Cancelar(long id, [FromBody] CancelarOrdenDto dto)
        {
            var transporte = await CargarAsync(id);
            var orden = transporte?.OrdenDetalle?.Orden;
            if (transporte == null || orden == null) return NotFound();

            if (!PuedeOperar(orden.Id_TipoOrden)) return SinPermisoTipo(orden.Id_TipoOrden);

            if (orden.FechaCierre != null)
                return BadRequest("La orden ya fue cerrada con sus pesadas y no puede cancelarse.");
            if (!transporte.Activo || !orden.Activo)
                return BadRequest("La orden ya está cancelada.");

            var motivo = dto.Motivo?.Trim() ?? "";
            if (motivo.Length == 0)
                return BadRequest("Indique el motivo de la cancelación.");
            if (motivo.Length > LargoMaximoMotivoAnulacion)
                return BadRequest($"El motivo de la cancelación admite hasta {LargoMaximoMotivoAnulacion} caracteres.");

            await LiberarTanqueAsync(transporte.OrdenDetalle);

            orden.Activo = false;
            orden.Id_EstadoOrden = EstadoOrdenAnulado;
            orden.FechaAnulacion = DateTime.Now;
            orden.MotivoAnulacion = motivo;
            transporte.Activo = false;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Orden cancelada." });
        }

        // ─── Eliminar (CU065 / CU071 / CU076) ───────────────────────────────────
        // Baja física en cascada. Si la OT ya tiene pesadas, la FK de t_Pesada hace fallar el
        // SaveChanges y ForeignKeyConflictHandler lo devuelve como 409 con un mensaje claro
        // (no se atrapa la excepción acá). La transacción se descarta sin confirmar.
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            var transporte = await CargarAsync(id);
            if (transporte == null) return NotFound();

            var tipoOrden = transporte.OrdenDetalle?.Orden?.Id_TipoOrden ?? TipoOrdenEntrega;
            if (!PuedeOperar(tipoOrden)) return SinPermisoTipo(tipoOrden);

            // 0. Liberar y eliminar el vínculo con el tanque (si no, la baja falla por clave foránea)
            if (transporte.OrdenDetalle != null)
            {
                await LiberarTanqueAsync(transporte.OrdenDetalle);
                var vinculosTanque = await _context.TanqueAlmacenamientos
                    .Where(ta => ta.Id_OrdenDetalle == transporte.OrdenDetalle.Id_OrdenDetalle)
                    .ToListAsync();
                _context.TanqueAlmacenamientos.RemoveRange(vinculosTanque);
            }

            // 1. Planificación
            var plan = await _context.Planificaciones.FirstOrDefaultAsync(p => p.Id_OrdenTransporte == id);
            if (plan != null) _context.Planificaciones.Remove(plan);

            // 2. Orden de transporte
            _context.OrdenTransportes.Remove(transporte);

            // 3. Detalle y 4. Orden
            if (transporte.OrdenDetalle != null)
            {
                _context.OrdenDetalles.Remove(transporte.OrdenDetalle);
                if (transporte.OrdenDetalle.Orden != null)
                {
                    _context.Ordenes.Remove(transporte.OrdenDetalle.Orden);
                }
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return NoContent();
        }
    }

    public class CancelarOrdenDto
    {
        public string? Motivo { get; set; }
    }

    public class ReplanificarOrdenDto
    {
        public DateTime? Fecha { get; set; }
        public string? Motivo { get; set; }
    }

    public class OrdenTransporteCreationDto
    {
        public int Id_Producto { get; set; }
        public int Id_RazonSocialCliente { get; set; }
        public int Id_RazonSocialTransportista { get; set; }
        public int Id_Vehiculo { get; set; }
        public int Id_Conductor { get; set; }
        public int Id_PuestoExpedicionRecepcion { get; set; }
        public long NroRemito { get; set; }
        public decimal Cantidad { get; set; }
        // Solo se usa en el alta: después, la fecha se cambia con Replanificar.
        public DateTime Fecha { get; set; }
        public byte Id_TipoOrden { get; set; }
        public int? Id_Tanque { get; set; }
    }
}
