using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Backend.Auth
{
    // Claims del JWT que emite AuthController.
    public static class ClaimsCilp
    {
        public const string IdUsuario = "sub";
        public const string IdRed = "name";
        public const string Rol = "role";
        public const string Punto = "punto";
        public const string NombreCompleto = "nombre";
    }

    public static class Permisos
    {
        public static bool TienePunto(this ClaimsPrincipal usuario, int punto) =>
            usuario.HasClaim(ClaimsCilp.Punto, punto.ToString());

        public static bool TieneAlguno(this ClaimsPrincipal usuario, IEnumerable<int> puntos) =>
            puntos.Any(usuario.TienePunto);

        public static int? IdUsuario(this ClaimsPrincipal usuario) =>
            int.TryParse(usuario.FindFirstValue(ClaimsCilp.IdUsuario), out var id) ? id : null;

        // 403 con el mismo formato (ProblemDetails) que usa el resto de la API.
        public static ObjectResult Prohibido(string detalle) =>
            new(new ProblemDetails { Status = StatusCodes.Status403Forbidden, Title = "Sin permiso", Detail = detalle })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
    }

    // Exige que el usuario logueado tenga alguno de los puntos de menú indicados (por sus roles).
    //  - Sin puntos: alcanza con estar logueado.
    //  - LecturaLibre: los GET quedan abiertos a cualquier usuario logueado (los maestros
    //    alimentan los combos de otras pantallas); lo que escribe exige el punto.
    // Si la acción tiene su propio [Permiso], ése reemplaza al del controller.
    // Que el usuario esté logueado lo exige antes la política por defecto (Program.cs): 401.
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public class PermisoAttribute : Attribute, IAuthorizationFilter
    {
        public int[] Puntos { get; }
        public bool LecturaLibre { get; set; }

        public PermisoAttribute(params int[] puntos)
        {
            Puntos = puntos;
        }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var metadata = context.ActionDescriptor.EndpointMetadata;
            if (!ReferenceEquals(metadata.OfType<PermisoAttribute>().Last(), this)) return;
            if (metadata.OfType<IAllowAnonymous>().Any()) return;

            var usuario = context.HttpContext.User;
            if (usuario.Identity?.IsAuthenticated != true) return;
            if (Puntos.Length == 0) return;
            if (LecturaLibre && HttpMethods.IsGet(context.HttpContext.Request.Method)) return;
            if (usuario.TieneAlguno(Puntos)) return;

            context.Result = Permisos.Prohibido("No tiene permiso para realizar esta operación. Consulte al administrador del sistema.");
        }
    }
}
