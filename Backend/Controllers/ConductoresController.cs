using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend.Auth;
using Backend.Data;
using Backend.Models;

namespace Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Permiso(PuntoMenu.Conductores, LecturaLibre = true)]
    public class ConductoresController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ConductoresController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Conductor>>> GetConductores()
        {
            return await _context.Conductores.Where(c => c.Activo).OrderBy(c => c.Id_Conductor).ToListAsync();
        }

        // El tipo de documento tiene que existir y estar activo (t_TipoDocumentoIdentidad).
        private Task<bool> TipoDocumentoValidoAsync(byte idTipo) =>
            _context.TiposDocumentoIdentidad.AnyAsync(t => t.Id_TipoDocumentoIdentidad == idTipo && t.Activo);

        private const string TipoDocumentoInvalido = "El tipo de documento no existe o no está activo.";

        [HttpPost]
        public async Task<ActionResult<Conductor>> PostConductor(Conductor conductor)
        {
            if (!await TipoDocumentoValidoAsync(conductor.Id_TipoDocumento))
                return BadRequest(TipoDocumentoInvalido);

            _context.Conductores.Add(conductor);
            await _context.SaveChangesAsync();
            return CreatedAtAction("GetConductor", new { id = conductor.Id_Conductor }, conductor);
        }
        [HttpGet("{id}")]
        public async Task<ActionResult<Conductor>> GetConductor(int id)
        {
            var item = await _context.Conductores.FindAsync(id);
            if (item == null) return NotFound();
            return item;
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutConductor(int id, Conductor conductor)
        {
            if (id != conductor.Id_Conductor)
            {
                return BadRequest();
            }

            if (!await TipoDocumentoValidoAsync(conductor.Id_TipoDocumento))
                return BadRequest(TipoDocumentoInvalido);

            _context.Entry(conductor).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Conductores.Any(e => e.Id_Conductor == id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteConductor(int id)
        {
            var conductor = await _context.Conductores.FindAsync(id);
            if (conductor == null)
            {
                return NotFound();
            }

            conductor.Activo = false;
            _context.Entry(conductor).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
