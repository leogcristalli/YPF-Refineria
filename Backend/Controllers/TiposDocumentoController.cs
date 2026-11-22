using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend.Auth;
using Backend.Data;

namespace Backend.Controllers
{
    // Tipos de documento de identidad (sysdata t_TipoDocumentoIdentidad): solo lectura.
    // Abierto a cualquier usuario logueado: alimenta los combos de los formularios.
    [Route("api/[controller]")]
    [ApiController]
    [Permiso]
    public class TiposDocumentoController : ControllerBase
    {
        private readonly AppDbContext _context;

        public TiposDocumentoController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/TiposDocumento
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> Get()
        {
            return Ok(await _context.TiposDocumentoIdentidad
                .AsNoTracking()
                .Where(t => t.Activo)
                .OrderBy(t => t.Id_TipoDocumentoIdentidad)
                .Select(t => new { t.Id_TipoDocumentoIdentidad, t.Descripcion })
                .ToListAsync());
        }
    }
}
