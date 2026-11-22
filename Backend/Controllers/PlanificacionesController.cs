using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend.Auth;
using Backend.Data;
using Backend.Models;

namespace Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Permiso]
    public class PlanificacionesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public PlanificacionesController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Planificacion>>> Get()
        {
            return await _context.Planificaciones
                .Include(p => p.OrdenTransporte)
                    .ThenInclude(ot => ot.OrdenDetalle)
                        .ThenInclude(od => od.Producto)
                .Include(p => p.OrdenTransporte)
                    .ThenInclude(ot => ot.Vehiculo)
                .ToListAsync();
        }

        // GET: api/Planificaciones/pendientes
        // Solo las de OT abiertas con pesadas pendientes: son las que se pueden pesar (modal de CU078).
        [HttpGet("pendientes")]
        [Permiso(PuntoMenu.Pesadas)]
        public async Task<ActionResult<IEnumerable<Planificacion>>> GetPendientes()
        {
            return await _context.PendientesDePesaje()
                .Include(p => p.OrdenTransporte)
                    .ThenInclude(ot => ot!.OrdenDetalle)
                        .ThenInclude(od => od!.Producto)
                .Include(p => p.OrdenTransporte)
                    .ThenInclude(ot => ot!.Vehiculo)
                .OrderBy(p => p.OrdenTransporte!.FechaPlanificacion)
                .ThenBy(p => p.Id_Planificacion)
                .ToListAsync();
        }

        [HttpGet("{id:long}")]
        public async Task<ActionResult<Planificacion>> Get(long id)
        {
            var item = await _context.Planificaciones.FindAsync(id);
            if (item == null) return NotFound();
            return item;
        }
    }
}
