using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Backend.Auth;
using Backend.Data;
using Backend.Models;

namespace Backend.Controllers
{
    // Inicio de sesión: valida ID de red y contraseña y devuelve un JWT con el usuario,
    // sus roles y los puntos de menú que esos roles tienen asignados.
    // Los permisos viajan en el token: un cambio de roles se aplica al volver a iniciar sesión.
    [Route("api/[controller]")]
    [ApiController]
    [AllowAnonymous]
    public class AuthController : ControllerBase
    {
        private const string CredencialesInvalidas = "Usuario o contraseña incorrectos.";

        private readonly AppDbContext _context;
        private readonly IConfiguration _config;
        private readonly PasswordHasher<Usuario> _hasher = new();

        public AuthController(AppDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        public class LoginRequest
        {
            public string? IdRed { get; set; }
            public string? Password { get; set; }
        }

        // POST: api/Auth/login
        [HttpPost("login")]
        public async Task<ActionResult<object>> Login(LoginRequest datos)
        {
            var idRed = (datos.IdRed ?? "").Trim();
            if (idRed.Length == 0 || string.IsNullOrEmpty(datos.Password))
                return NoAutorizado();

            // Mismo mensaje si el usuario no existe, está dado de baja o la contraseña no coincide.
            var usuario = await _context.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.IdRed == idRed && u.Activo);
            if (usuario == null
                || _hasher.VerifyHashedPassword(usuario, usuario.PasswordHash, datos.Password) == PasswordVerificationResult.Failed)
                return NoAutorizado();

            var roles = await (
                from ur in _context.UsuarioRoles
                join r in _context.Roles on ur.Id_Rol equals r.Id_Rol
                where ur.Id_Usuario == usuario.Id_Usuario && ur.Activo && r.Activo
                orderby r.Id_Rol
                select new { r.Id_Rol, r.Descripcion }).ToListAsync();

            if (roles.Count == 0)
                return Permisos.Prohibido("El usuario no tiene roles asignados. Consulte al administrador del sistema.");

            var idsRol = roles.Select(r => r.Id_Rol).ToList();
            var puntos = await (
                from rc in _context.RolCasosDeUso
                join c in _context.CasosDeUso on rc.Id_CasoDeUso equals c.Id_CasoDeUso
                where idsRol.Contains(rc.Id_Rol) && rc.Activo && c.Activo
                select c.Id_CasoDeUso).Distinct().OrderBy(id => id).ToListAsync();

            var expira = DateTime.UtcNow.AddHours(_config.GetValue("Jwt:HorasVigencia", 8));

            var claims = new List<Claim>
            {
                new(ClaimsCilp.IdUsuario, usuario.Id_Usuario.ToString()),
                new(ClaimsCilp.IdRed, usuario.IdRed),
                new(ClaimsCilp.NombreCompleto, $"{usuario.Nombre} {usuario.Apellido}")
            };
            claims.AddRange(roles.Select(r => new Claim(ClaimsCilp.Rol, r.Descripcion)));
            claims.AddRange(puntos.Select(p => new Claim(ClaimsCilp.Punto, p.ToString())));

            var clave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
            var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
            {
                Issuer = _config["Jwt:Issuer"],
                Audience = _config["Jwt:Audience"],
                Subject = new ClaimsIdentity(claims),
                Expires = expira,
                SigningCredentials = new SigningCredentials(clave, SecurityAlgorithms.HmacSha256)
            });

            return Ok(new
            {
                token,
                expira,
                usuario = new { usuario.Id_Usuario, usuario.IdRed, usuario.Nombre, usuario.Apellido },
                roles,
                puntosMenu = puntos
            });
        }

        private ObjectResult NoAutorizado() =>
            new(new ProblemDetails { Status = StatusCodes.Status401Unauthorized, Title = "No autorizado", Detail = CredencialesInvalidas })
            {
                StatusCode = StatusCodes.Status401Unauthorized
            };
    }
}
