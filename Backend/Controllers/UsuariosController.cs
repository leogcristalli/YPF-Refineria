using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend.Auth;
using Backend.Data;
using Backend.Models;

namespace Backend.Controllers
{
    // ABM de Usuarios (CU10 a CU14) y asignación de roles (t_UsuarioRol).
    [Route("api/[controller]")]
    [ApiController]
    [Permiso(PuntoMenu.Usuarios)]
    public class UsuariosController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly PasswordHasher<Usuario> _hasher = new();

        public UsuariosController(AppDbContext context)
        {
            _context = context;
        }

        // Datos editables de un usuario. Id, Activo y el hash de la contraseña no se aceptan:
        // el id va en la URL, la baja es el DELETE y el hash lo calcula el backend.
        public class UsuarioRequest
        {
            // Acepta usuarios de dominio (ej. YPF\ADMIN).
            [Required(ErrorMessage = "El ID de red es requerido.")]
            [StringLength(50, MinimumLength = 3, ErrorMessage = "El ID de red debe tener entre 3 y 50 caracteres.")]
            // Ojo: .NET rechaza escapes innecesarios como \_ (RegexParseException -> 500).
            [RegularExpression(@"^[a-zA-Z0-9._\\-]+$", ErrorMessage = "El ID de red solo puede contener letras, números, puntos, guiones y \\.")]
            public string IdRed { get; set; } = "";

            [Required(ErrorMessage = "El nombre es requerido.")]
            [StringLength(100, MinimumLength = 2, ErrorMessage = "El nombre debe tener entre 2 y 100 caracteres.")]
            [RegularExpression(@"^[a-zA-ZáéíóúÁÉÍÓÚüÜñÑ\s]+$", ErrorMessage = "El nombre solo puede contener letras.")]
            public string Nombre { get; set; } = "";

            [Required(ErrorMessage = "El apellido es requerido.")]
            [StringLength(100, MinimumLength = 2, ErrorMessage = "El apellido debe tener entre 2 y 100 caracteres.")]
            [RegularExpression(@"^[a-zA-ZáéíóúÁÉÍÓÚüÜñÑ\s]+$", ErrorMessage = "El apellido solo puede contener letras.")]
            public string Apellido { get; set; } = "";

            [Required(ErrorMessage = "El número de documento es requerido.")]
            [StringLength(12, MinimumLength = 6, ErrorMessage = "El documento debe tener entre 6 y 12 caracteres.")]
            [RegularExpression(@"^[a-zA-Z0-9]+$", ErrorMessage = "El número de documento debe ser alfanumérico, sin espacios.")]
            public string NroDocumento { get; set; } = "";

            [Range(1, byte.MaxValue, ErrorMessage = "El tipo de documento es requerido.")]
            public byte Id_TipoDocumento { get; set; }

            // Obligatoria en el alta; en la edición, vacía = conservar la actual.
            public string? Password { get; set; }
        }

        public class RolesUsuarioRequest
        {
            public List<int>? Roles { get; set; }
        }

        // Usuarios activos con la descripción de sus roles activos.
        private IQueryable<object> Proyeccion(IQueryable<Usuario> usuarios) =>
            usuarios.Select(u => new
            {
                u.Id_Usuario,
                u.IdRed,
                u.Nombre,
                u.Apellido,
                u.NroDocumento,
                u.Id_TipoDocumento,
                u.Activo,
                Roles = (from ur in _context.UsuarioRoles
                         join r in _context.Roles on ur.Id_Rol equals r.Id_Rol
                         where ur.Id_Usuario == u.Id_Usuario && ur.Activo && r.Activo
                         orderby r.Id_Rol
                         select r.Descripcion).ToList()
            });

        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> Get()
        {
            return Ok(await Proyeccion(_context.Usuarios.Where(u => u.Activo).OrderBy(u => u.Id_Usuario)).ToListAsync());
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<object>> Get(int id)
        {
            var item = await Proyeccion(_context.Usuarios.Where(u => u.Id_Usuario == id && u.Activo)).FirstOrDefaultAsync();
            if (item == null) return NotFound();
            return Ok(item);
        }

        // Devuelve un mensaje de error o null si los datos son válidos.
        private async Task<string?> ValidarAsync(UsuarioRequest datos, int idActual)
        {
            datos.IdRed = datos.IdRed.Trim();
            datos.Nombre = datos.Nombre.Trim();
            datos.Apellido = datos.Apellido.Trim();
            datos.NroDocumento = datos.NroDocumento.Trim();

            // El índice único UX_Usuario_IdRed incluye a los usuarios dados de baja.
            var existente = await _context.Usuarios.AsNoTracking()
                .FirstOrDefaultAsync(u => u.IdRed == datos.IdRed && u.Id_Usuario != idActual);
            if (existente != null)
                return existente.Activo
                    ? $"Ya existe un usuario con el ID de red {datos.IdRed}."
                    : $"El ID de red {datos.IdRed} pertenece a un usuario dado de baja y no puede reutilizarse.";

            if (!await _context.TiposDocumentoIdentidad.AnyAsync(t => t.Id_TipoDocumentoIdentidad == datos.Id_TipoDocumento && t.Activo))
                return "El tipo de documento no existe o no está activo.";

            return null;
        }

        // CU11: Crear Usuario
        [HttpPost]
        public async Task<ActionResult<object>> Post(UsuarioRequest datos)
        {
            if (string.IsNullOrWhiteSpace(datos.Password))
                return BadRequest("La contraseña es obligatoria.");

            var error = await ValidarAsync(datos, 0);
            if (error != null) return BadRequest(error);

            var usuario = new Usuario
            {
                IdRed = datos.IdRed,
                Nombre = datos.Nombre,
                Apellido = datos.Apellido,
                NroDocumento = datos.NroDocumento,
                Id_TipoDocumento = datos.Id_TipoDocumento,
                Activo = true
            };
            usuario.PasswordHash = _hasher.HashPassword(usuario, datos.Password);

            _context.Usuarios.Add(usuario);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(Get), new { id = usuario.Id_Usuario },
                await Proyeccion(_context.Usuarios.Where(u => u.Id_Usuario == usuario.Id_Usuario)).FirstAsync());
        }

        // CU13: Modificar Usuario. Contraseña vacía = conservar la actual.
        [HttpPut("{id}")]
        public async Task<IActionResult> Put(int id, UsuarioRequest datos)
        {
            var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Id_Usuario == id && u.Activo);
            if (usuario == null) return NotFound();

            var error = await ValidarAsync(datos, id);
            if (error != null) return BadRequest(error);

            usuario.IdRed = datos.IdRed;
            usuario.Nombre = datos.Nombre;
            usuario.Apellido = datos.Apellido;
            usuario.NroDocumento = datos.NroDocumento;
            usuario.Id_TipoDocumento = datos.Id_TipoDocumento;
            if (!string.IsNullOrWhiteSpace(datos.Password))
                usuario.PasswordHash = _hasher.HashPassword(usuario, datos.Password);

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // CU14: Eliminar Usuario (baja lógica)
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Id_Usuario == id && u.Activo);
            if (usuario == null) return NotFound();

            if (User.IdUsuario() == id)
                return BadRequest("No puede darse de baja a sí mismo.");

            if (await EsUltimoAdministradorAsync(id))
                return BadRequest("No se puede dar de baja al último usuario con el rol Administrador de Sistema.");

            usuario.Activo = false;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // GET: api/Usuarios/5/roles  -> ids de los roles activos asignados
        [HttpGet("{id}/roles")]
        public async Task<ActionResult<IEnumerable<int>>> GetRoles(int id)
        {
            if (!await _context.Usuarios.AnyAsync(u => u.Id_Usuario == id && u.Activo)) return NotFound();

            return await _context.UsuarioRoles
                .Where(ur => ur.Id_Usuario == id && ur.Activo)
                .Select(ur => ur.Id_Rol)
                .OrderBy(x => x)
                .ToListAsync();
        }

        // PUT: api/Usuarios/5/roles  { roles: [1, 7] }
        // Deja asignados exactamente esos roles (reactiva, agrega o da de baja).
        [HttpPut("{id}/roles")]
        public async Task<IActionResult> PutRoles(int id, RolesUsuarioRequest datos)
        {
            if (!await _context.Usuarios.AnyAsync(u => u.Id_Usuario == id && u.Activo)) return NotFound();

            var ids = (datos.Roles ?? new List<int>()).Distinct().ToList();
            var validos = await _context.Roles.CountAsync(r => r.Activo && ids.Contains(r.Id_Rol));
            if (validos != ids.Count)
                return BadRequest("Alguno de los roles seleccionados no existe o está dado de baja.");

            if (!ids.Contains(RolBase.AdministradorSistema) && await EsUltimoAdministradorAsync(id))
                return BadRequest("No se puede quitar el rol Administrador de Sistema al último usuario que lo tiene.");

            var existentes = await _context.UsuarioRoles.Where(ur => ur.Id_Usuario == id).ToListAsync();
            foreach (var asignacion in existentes)
                asignacion.Activo = ids.Contains(asignacion.Id_Rol);
            foreach (var idRol in ids.Where(r => !existentes.Any(x => x.Id_Rol == r)))
                _context.UsuarioRoles.Add(new UsuarioRol { Id_Usuario = id, Id_Rol = idRol, Activo = true });

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // El usuario tiene el rol Administrador de Sistema y no hay otro usuario activo que lo tenga.
        private async Task<bool> EsUltimoAdministradorAsync(int idUsuario)
        {
            var administradores = _context.UsuarioRoles
                .Where(ur => ur.Id_Rol == RolBase.AdministradorSistema && ur.Activo
                    && _context.Usuarios.Any(u => u.Id_Usuario == ur.Id_Usuario && u.Activo));

            return await administradores.AnyAsync(ur => ur.Id_Usuario == idUsuario)
                && !await administradores.AnyAsync(ur => ur.Id_Usuario != idUsuario);
        }
    }
}
