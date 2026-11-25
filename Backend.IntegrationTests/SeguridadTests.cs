using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Backend.Auth;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Backend.IntegrationTests;

/// <summary>Batería "Seguridad": login, tokens, permisos por rol y protecciones de administrador.</summary>
[Collection(ColeccionApi.Nombre)]
public class SeguridadTests : IClassFixture<CilpFactory>
{
    private readonly CilpFactory _f;
    public SeguridadTests(CilpFactory f) { _f = f; BaseDatos.Restablecer(); }

    // ───────────── Login ─────────────
    [Fact] // SEG-04 / PS-01
    public async Task Login_correcto_devuelve_token()
    {
        var r = await _f.Cliente().PostAsJsonAsync("/api/Auth/login", new { idRed = Api.Operador, password = Api.Clave });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        using var doc = await Jx.Leer(r);
        Assert.False(string.IsNullOrWhiteSpace(Jx.Texto(doc.RootElement, "token")));
    }

    [Fact] // SEG-01 / PS-02
    public async Task Login_con_password_incorrecta_responde_401()
    {
        var r = await _f.Cliente().PostAsJsonAsync("/api/Auth/login", new { idRed = Api.Admin, password = "incorrecta" });
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [Fact]
    public async Task Login_con_usuario_inexistente_responde_401()
    {
        var r = await _f.Cliente().PostAsJsonAsync("/api/Auth/login", new { idRed = "YPF\\NO_EXISTE", password = Api.Clave });
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [Fact] // PS-02: el mensaje no revela si el usuario existe
    public async Task Usuario_inexistente_y_password_errada_dan_el_mismo_mensaje()
    {
        var a = await _f.Cliente().PostAsJsonAsync("/api/Auth/login", new { idRed = Api.Admin, password = "mala" });
        var b = await _f.Cliente().PostAsJsonAsync("/api/Auth/login", new { idRed = "YPF\\FANTASMA", password = "mala" });
        Assert.Equal(await a.Content.ReadAsStringAsync(), await b.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("", "x")]
    [InlineData("YPF\\ADMIN", "")]
    [InlineData("   ", "   ")]
    public async Task Login_con_campos_vacios_responde_401(string idRed, string password)
    {
        var r = await _f.Cliente().PostAsJsonAsync("/api/Auth/login", new { idRed, password });
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [Fact] // PS-03
    public async Task Usuario_sin_roles_no_inicia_sesion()
    {
        var admin = await Api.ComoAdmin(_f);
        var idRed = $"YPF\\SR{Guid.NewGuid():N}"[..12];
        var alta = await admin.PostAsJsonAsync("/api/Usuarios", NuevoUsuario(idRed));
        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);

        var r = await _f.Cliente().PostAsJsonAsync("/api/Auth/login", new { idRed, password = Api.Clave });
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    // ───────────── Tokens ─────────────
    [Fact] // SEG-05 / PS-05
    public async Task Sin_token_la_api_responde_401()
    {
        var r = await _f.Cliente().GetAsync("/api/Tanques");
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [Fact]
    public async Task Token_basura_responde_401()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await ConToken("esto.no.es.un.jwt").GetAsync("/api/Tanques")).StatusCode);
    }

    [Fact]
    public async Task Token_vencido_responde_401()
    {
        var token = Token(CilpFactory.ClaveJwt, DateTime.UtcNow.AddMinutes(-10));
        Assert.Equal(HttpStatusCode.Unauthorized, (await ConToken(token).GetAsync("/api/Tanques")).StatusCode);
    }

    [Fact]
    public async Task Token_firmado_con_otra_clave_responde_401()
    {
        var token = Token("otra-clave-distinta-de-treinta-y-dos-caracteres!!", DateTime.UtcNow.AddHours(1));
        Assert.Equal(HttpStatusCode.Unauthorized, (await ConToken(token).GetAsync("/api/Tanques")).StatusCode);
    }

    [Fact]
    public async Task Token_con_otra_audiencia_responde_401()
    {
        var token = Token(CilpFactory.ClaveJwt, DateTime.UtcNow.AddHours(1), audience: "OTRO");
        Assert.Equal(HttpStatusCode.Unauthorized, (await ConToken(token).GetAsync("/api/Tanques")).StatusCode);
    }

    [Fact]
    public async Task Token_valido_con_los_permisos_correctos_es_aceptado()
    {
        var token = Token(CilpFactory.ClaveJwt, DateTime.UtcNow.AddHours(1), puntos: new[] { PuntoMenu.Usuarios });
        Assert.Equal(HttpStatusCode.OK, (await ConToken(token).GetAsync("/api/Usuarios")).StatusCode);
    }

    [Fact]
    public async Task Token_valido_sin_el_punto_de_menu_responde_403()
    {
        var token = Token(CilpFactory.ClaveJwt, DateTime.UtcNow.AddHours(1), puntos: new[] { PuntoMenu.Pesadas });
        Assert.Equal(HttpStatusCode.Forbidden, (await ConToken(token).GetAsync("/api/Usuarios")).StatusCode);
    }

    // ───────────── Permisos por rol ─────────────
    [Theory] // SEG-05 / PS-04
    [InlineData("/api/Usuarios")]
    [InlineData("/api/PuntosMenu")]
    public async Task Operador_no_accede_a_pantallas_de_administracion(string url)
    {
        var c = await Api.ComoOperador(_f);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task Operador_no_puede_modificar_maestros_pero_si_leerlos()
    {
        var c = await Api.ComoOperador(_f);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/Productos")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.PostAsJsonAsync("/api/Productos", new { descripcion = "X" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.DeleteAsync("/api/Tanques/1")).StatusCode);
    }

    [Fact] // SEG-02
    public async Task Administrador_accede_a_todas_las_pantallas()
    {
        var c = await Api.ComoAdmin(_f);
        foreach (var url in new[] { "/api/Usuarios", "/api/Roles", "/api/PuntosMenu", "/api/Tanques", "/api/Vehiculos",
                                    "/api/Basculas", "/api/Conductores", "/api/Puestos", "/api/Reportes/kpis" })
            Assert.True((await c.GetAsync(url)).IsSuccessStatusCode, url);
    }

    [Fact] // SEG-03
    public async Task Alta_de_usuario_con_rol_Administrador_de_Recepcion_inicia_sesion_con_ese_rol()
    {
        var admin = await Api.ComoAdmin(_f);
        var idRed = $"YPF\\AR{Guid.NewGuid():N}"[..12];
        var alta = await admin.PostAsJsonAsync("/api/Usuarios", NuevoUsuario(idRed));
        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        var idUsuario = await Datos.IdUsuario(admin, idRed);
        var idRol = await Datos.IdRol(admin, "Administrador de Recepción");
        Assert.True((await admin.PutAsJsonAsync($"/api/Usuarios/{idUsuario}/roles", new { roles = new[] { (int)idRol } })).IsSuccessStatusCode);

        var login = await _f.Cliente().PostAsJsonAsync("/api/Auth/login", new { idRed, password = Api.Clave });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Contains("Recepción", await login.Content.ReadAsStringAsync());
    }

    [Fact] // SEG-06 / defecto de permisos por tipo de orden
    public async Task Administrador_de_Recepcion_no_puede_crear_ordenes_de_entrega()
    {
        var admin = await Api.ComoAdmin(_f);
        var idRed = $"YPF\\PT{Guid.NewGuid():N}"[..12];
        await admin.PostAsJsonAsync("/api/Usuarios", NuevoUsuario(idRed));
        var idUsuario = await Datos.IdUsuario(admin, idRed);
        var idRol = await Datos.IdRol(admin, "Administrador de Recepción");
        await admin.PutAsJsonAsync($"/api/Usuarios/{idUsuario}/roles", new { roles = new[] { (int)idRol } });

        var recepcion = await Api.Sesion(_f, idRed);
        var entrega = await recepcion.PostAsJsonAsync("/api/OrdenesTransporte", new { id_TipoOrden = (byte)1 });
        Assert.Equal(HttpStatusCode.Forbidden, entrega.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await recepcion.GetAsync("/api/Usuarios")).StatusCode);
    }

    // ───────────── Protecciones de administrador ─────────────
    [Fact] // PM-06
    public async Task Un_usuario_no_puede_darse_de_baja_a_si_mismo()
    {
        var admin = await Api.ComoAdmin(_f);
        var id = await Datos.IdUsuario(admin, Api.Admin);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.DeleteAsync($"/api/Usuarios/{id}")).StatusCode);
    }

    [Fact] // PM-06
    public async Task No_se_quita_el_rol_Administrador_de_Sistema_al_ultimo_usuario_que_lo_tiene()
    {
        var admin = await Api.ComoAdmin(_f);
        var id = await Datos.IdUsuario(admin, Api.Admin);
        var otrosRoles = Enumerable.Range(2, 7).ToArray();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/Usuarios/{id}/roles", new { roles = otrosRoles })).StatusCode);
    }

    [Fact]
    public async Task Asignar_un_rol_inexistente_se_rechaza()
    {
        var admin = await Api.ComoAdmin(_f);
        var id = await Datos.IdUsuario(admin, Api.Operador);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/Usuarios/{id}/roles", new { roles = new[] { 9999 } })).StatusCode);
    }

    // ───────────── Auditoría ─────────────
    [Fact] // SEG-08 / defecto 6: la auditoría guarda el usuario exacto, sin caracteres de relleno
    [Trait("Defecto", "6")]
    public async Task La_auditoria_registra_al_usuario_de_la_aplicacion_sin_relleno()
    {
        var c = await Api.ComoOperador(_f);
        var plan = await Datos.PlanificacionPendiente(c, "AE123BC");
        Assert.Equal(HttpStatusCode.Created, (await Datos.Pesar(c, plan, Datos.Tara, 15200m)).StatusCode);

        var textos = BaseDatos.TextosUltimaFila("dbo.t_Pesada", "Id_Pesada");
        Assert.Contains(Api.Operador, textos);
    }

    // ───────────── Auxiliares ─────────────
    internal static object NuevoUsuario(string idRed) => new
    {
        idRed, nombre = "Prueba", apellido = "Automatica",
        nroDocumento = Random.Shared.Next(10_000_000, 99_999_999).ToString(),
        id_TipoDocumento = (byte)1, password = Api.Clave
    };

    private HttpClient ConToken(string token)
    {
        var c = _f.Cliente();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return c;
    }

    private static string Token(string clave, DateTime vence, string audience = "CILP", int[]? puntos = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimsCilp.IdUsuario, "1"), new(ClaimsCilp.IdRed, Api.Admin),
            new(ClaimsCilp.Rol, "Administrador de Sistema")
        };
        foreach (var p in puntos ?? new[] { PuntoMenu.Usuarios })
            claims.Add(new Claim(ClaimsCilp.Punto, p.ToString()));

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "CILP", Audience = audience,
            Subject = new ClaimsIdentity(claims),
            NotBefore = vence.AddHours(-8), Expires = vence,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(clave)), SecurityAlgorithms.HmacSha256)
        });
    }
}
