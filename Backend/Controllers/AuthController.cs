using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
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
    //
    // Doble factor (TOTP): los usuarios con alguno de los roles de Auth:DobleFactor:RolesObligatorios
    // (perfiles administrativos) deben usarlo, y quien ya lo configuró siempre lo usa. En ese caso el
    // login con contraseña NO devuelve la sesión sino un "desafío": un token de 5 minutos, firmado con
    // otra clave y otra audiencia (el resto de la API no lo acepta), que solo sirve para los endpoints
    // de /2fa. La sesión recién se emite al verificar el código de la aplicación autenticadora.
    [Route("api/[controller]")]
    [ApiController]
    [AllowAnonymous]
    public class AuthController : ControllerBase
    {
        private const string CredencialesInvalidas = "Usuario o contraseña incorrectos.";
        private const string AudienciaDesafio = "CILP-2FA";
        private const string EmisorQr = "CILP Refinería";
        private const int MaxIntentos2fa = 5;
        private static readonly TimeSpan Bloqueo2fa = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan VigenciaDesafio = TimeSpan.FromMinutes(5);

        private readonly AppDbContext _context;
        private readonly IConfiguration _config;
        private readonly ProtectorTotp _protector;
        private readonly IMemoryCache _cache;
        private readonly PasswordHasher<Usuario> _hasher = new();

        public AuthController(AppDbContext context, IConfiguration config, ProtectorTotp protector, IMemoryCache cache)
        {
            _context = context;
            _config = config;
            _protector = protector;
            _cache = cache;
        }

        public class LoginRequest
        {
            public string? IdRed { get; set; }
            public string? Password { get; set; }
        }

        public class DesafioRequest
        {
            public string? Desafio { get; set; }
        }

        public class CodigoRequest
        {
            public string? Desafio { get; set; }
            public string? Codigo { get; set; }
        }

        private record RolInfo(int Id_Rol, string Descripcion);
        private record DatosSesion(List<RolInfo> Roles, List<int> Puntos);

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

            var sesion = await CargarSesionAsync(usuario.Id_Usuario);
            if (sesion.Roles.Count == 0)
                return Permisos.Prohibido("El usuario no tiene roles asignados. Consulte al administrador del sistema.");

            if (DobleFactorHabilitado)
            {
                // Quien ya configuró su autenticador lo usa siempre; los perfiles obligados deben configurarlo.
                if (usuario.TotpHabilitado)
                    return Ok(new { requiere2fa = true, desafio = CrearDesafio(usuario.Id_Usuario) });

                if (sesion.Roles.Any(r => RolesConDobleFactor.Contains(r.Descripcion, StringComparer.OrdinalIgnoreCase)))
                    return Ok(new { requiereEnrolamiento = true, desafio = CrearDesafio(usuario.Id_Usuario) });
            }

            return Ok(EmitirSesion(usuario, sesion));
        }

        // POST: api/Auth/2fa/verificar  (ingreso con el código de 6 dígitos de la aplicación autenticadora)
        [HttpPost("2fa/verificar")]
        public async Task<ActionResult<object>> Verificar2fa(CodigoRequest datos)
        {
            var idUsuario = await LeerDesafioAsync(datos.Desafio);
            if (idUsuario == null) return DesafioVencido();

            var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Id_Usuario == idUsuario && u.Activo);
            if (usuario == null || !usuario.TotpHabilitado) return DesafioVencido();

            var bloqueo = ControlarIntentos(usuario.Id_Usuario);
            if (bloqueo != null) return bloqueo;

            var secreto = _protector.Revelar(usuario.TotpSecreto);
            if (secreto == null || !Totp.Verificar(secreto, datos.Codigo, usuario.TotpUltimoPaso, DateTimeOffset.UtcNow, out var paso))
                return CodigoIncorrecto(usuario.Id_Usuario);

            usuario.TotpUltimoPaso = paso;
            await _context.SaveChangesAsync();
            _cache.Remove(ClaveIntentos(usuario.Id_Usuario));

            return await EmitirSesionAsync(usuario);
        }

        // POST: api/Auth/2fa/enrolar/iniciar  (genera el secreto y devuelve lo que muestra el código QR)
        [HttpPost("2fa/enrolar/iniciar")]
        public async Task<ActionResult<object>> IniciarEnrolamiento(DesafioRequest datos)
        {
            var idUsuario = await LeerDesafioAsync(datos.Desafio);
            if (idUsuario == null) return DesafioVencido();

            var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Id_Usuario == idUsuario && u.Activo);
            if (usuario == null) return DesafioVencido();

            // Nunca se pisa un autenticador ya configurado: para cambiarlo, el administrador lo restablece.
            if (usuario.TotpHabilitado)
                return BadRequest("El usuario ya tiene configurado el doble factor de autenticación.");

            var secreto = Totp.GenerarSecreto();
            usuario.TotpSecreto = _protector.Proteger(secreto);   // queda pendiente hasta confirmar con un código
            usuario.TotpHabilitado = false;
            usuario.TotpUltimoPaso = null;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                secreto = string.Join(' ', Enumerable.Range(0, secreto.Length / 4).Select(i => secreto.Substring(i * 4, 4))),
                uri = Totp.UriOtpAuth(EmisorQr, usuario.IdRed, secreto)
            });
        }

        // POST: api/Auth/2fa/enrolar/confirmar  (el usuario prueba su autenticador y se activa el doble factor)
        [HttpPost("2fa/enrolar/confirmar")]
        public async Task<ActionResult<object>> ConfirmarEnrolamiento(CodigoRequest datos)
        {
            var idUsuario = await LeerDesafioAsync(datos.Desafio);
            if (idUsuario == null) return DesafioVencido();

            var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Id_Usuario == idUsuario && u.Activo);
            if (usuario == null || usuario.TotpHabilitado) return DesafioVencido();

            var secreto = _protector.Revelar(usuario.TotpSecreto);
            if (secreto == null)
                return BadRequest("No hay una configuración pendiente. Vuelva a iniciar el proceso.");

            var bloqueo = ControlarIntentos(usuario.Id_Usuario);
            if (bloqueo != null) return bloqueo;

            if (!Totp.Verificar(secreto, datos.Codigo, null, DateTimeOffset.UtcNow, out var paso))
                return CodigoIncorrecto(usuario.Id_Usuario);

            usuario.TotpHabilitado = true;
            usuario.TotpUltimoPaso = paso;
            await _context.SaveChangesAsync();
            _cache.Remove(ClaveIntentos(usuario.Id_Usuario));

            return await EmitirSesionAsync(usuario);
        }

        // ───────────── Configuración ─────────────

        private bool DobleFactorHabilitado => _config.GetValue("Auth:DobleFactor:Habilitado", true);

        private string[] RolesConDobleFactor =>
            _config.GetSection("Auth:DobleFactor:RolesObligatorios").Get<string[]>() ?? Array.Empty<string>();

        // ───────────── Sesión (JWT) ─────────────

        private async Task<DatosSesion> CargarSesionAsync(int idUsuario)
        {
            var roles = await (
                from ur in _context.UsuarioRoles
                join r in _context.Roles on ur.Id_Rol equals r.Id_Rol
                where ur.Id_Usuario == idUsuario && ur.Activo && r.Activo
                orderby r.Id_Rol
                select new RolInfo(r.Id_Rol, r.Descripcion)).ToListAsync();

            var idsRol = roles.Select(r => r.Id_Rol).ToList();
            var puntos = await (
                from rc in _context.RolCasosDeUso
                join c in _context.CasosDeUso on rc.Id_CasoDeUso equals c.Id_CasoDeUso
                where idsRol.Contains(rc.Id_Rol) && rc.Activo && c.Activo
                select c.Id_CasoDeUso).Distinct().OrderBy(id => id).ToListAsync();

            return new DatosSesion(roles, puntos);
        }

        private async Task<ActionResult<object>> EmitirSesionAsync(Usuario usuario)
        {
            var sesion = await CargarSesionAsync(usuario.Id_Usuario);
            if (sesion.Roles.Count == 0)
                return Permisos.Prohibido("El usuario no tiene roles asignados. Consulte al administrador del sistema.");

            return Ok(EmitirSesion(usuario, sesion));
        }

        private object EmitirSesion(Usuario usuario, DatosSesion sesion)
        {
            var expira = DateTime.UtcNow.AddHours(_config.GetValue("Jwt:HorasVigencia", 8));

            var claims = new List<Claim>
            {
                new(ClaimsCilp.IdUsuario, usuario.Id_Usuario.ToString()),
                new(ClaimsCilp.IdRed, usuario.IdRed),
                new(ClaimsCilp.NombreCompleto, $"{usuario.Nombre} {usuario.Apellido}")
            };
            claims.AddRange(sesion.Roles.Select(r => new Claim(ClaimsCilp.Rol, r.Descripcion)));
            claims.AddRange(sesion.Puntos.Select(p => new Claim(ClaimsCilp.Punto, p.ToString())));

            var clave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
            var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
            {
                Issuer = _config["Jwt:Issuer"],
                Audience = _config["Jwt:Audience"],
                Subject = new ClaimsIdentity(claims),
                Expires = expira,
                SigningCredentials = new SigningCredentials(clave, SecurityAlgorithms.HmacSha256)
            });

            return new
            {
                token,
                expira,
                usuario = new { usuario.Id_Usuario, usuario.IdRed, usuario.Nombre, usuario.Apellido },
                roles = sesion.Roles,
                puntosMenu = sesion.Puntos
            };
        }

        // ───────────── Desafío de doble factor ─────────────

        // Clave y audiencia propias: un desafío no sirve como token de sesión (el middleware de
        // autenticación valida contra Jwt:Key y Jwt:Audience y lo rechaza).
        private SymmetricSecurityKey ClaveDesafio() =>
            new(HMACSHA256.HashData(Encoding.UTF8.GetBytes("cilp-2fa-desafio"), Encoding.UTF8.GetBytes(_config["Jwt:Key"]!)));

        private string CrearDesafio(int idUsuario) =>
            new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
            {
                Issuer = _config["Jwt:Issuer"],
                Audience = AudienciaDesafio,
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim("sub", idUsuario.ToString()),
                    new Claim("prp", "2fa")
                }),
                Expires = DateTime.UtcNow.Add(VigenciaDesafio),
                SigningCredentials = new SigningCredentials(ClaveDesafio(), SecurityAlgorithms.HmacSha256)
            });

        private async Task<int?> LeerDesafioAsync(string? desafio)
        {
            if (string.IsNullOrWhiteSpace(desafio)) return null;

            var resultado = await new JsonWebTokenHandler().ValidateTokenAsync(desafio, new TokenValidationParameters
            {
                ValidIssuer = _config["Jwt:Issuer"],
                ValidAudience = AudienciaDesafio,
                IssuerSigningKey = ClaveDesafio(),
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateIssuerSigningKey = true,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            });

            if (!resultado.IsValid) return null;
            if (resultado.ClaimsIdentity.FindFirst("prp")?.Value != "2fa") return null;

            return int.TryParse(resultado.ClaimsIdentity.FindFirst("sub")?.Value, out var id) ? id : null;
        }

        // ───────────── Intentos fallidos ─────────────

        private static string ClaveIntentos(int idUsuario) => $"2fa-intentos:{idUsuario}";

        // Tras MaxIntentos2fa códigos incorrectos, el usuario queda bloqueado Bloqueo2fa (6 dígitos
        // se pueden adivinar por fuerza bruta si no se limitan los intentos).
        private ObjectResult? ControlarIntentos(int idUsuario) =>
            _cache.TryGetValue(ClaveIntentos(idUsuario), out int fallos) && fallos >= MaxIntentos2fa
                ? ErrorAuth(StatusCodes.Status429TooManyRequests, "Demasiados intentos",
                    $"Se superó el máximo de {MaxIntentos2fa} intentos. Espere {(int)Bloqueo2fa.TotalMinutes} minutos e inténtelo de nuevo.")
                : null;

        private ObjectResult CodigoIncorrecto(int idUsuario)
        {
            var fallos = _cache.TryGetValue(ClaveIntentos(idUsuario), out int previos) ? previos + 1 : 1;
            _cache.Set(ClaveIntentos(idUsuario), fallos, Bloqueo2fa);

            return ErrorAuth(StatusCodes.Status401Unauthorized, "Código incorrecto",
                "El código es incorrecto o ya fue usado. Verifique el código de su aplicación autenticadora.");
        }

        // ───────────── Errores ─────────────

        private static ObjectResult ErrorAuth(int estado, string titulo, string detalle) =>
            new(new ProblemDetails { Status = estado, Title = titulo, Detail = detalle }) { StatusCode = estado };

        private static ObjectResult NoAutorizado() =>
            ErrorAuth(StatusCodes.Status401Unauthorized, "No autorizado", CredencialesInvalidas);

        private static ObjectResult DesafioVencido() =>
            ErrorAuth(StatusCodes.Status401Unauthorized, "Verificación vencida",
                "La verificación venció o no es válida. Vuelva a iniciar sesión.");
    }
}