using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Backend.Auth;
using Xunit;

namespace Backend.IntegrationTests;

/// <summary>Batería "Seguridad" - doble factor (TOTP) de punta a punta contra la API.</summary>
[Collection(ColeccionApi.Nombre)]
public class DobleFactorTests : IClassFixture<CilpFactoryDobleFactor>, IClassFixture<CilpFactory>
{
    private readonly CilpFactoryDobleFactor _con2fa;   // como producción
    private readonly CilpFactory _sin2fa;              // para crear/restablecer usuarios de prueba

    public DobleFactorTests(CilpFactoryDobleFactor con2fa, CilpFactory sin2fa)
    {
        _con2fa = con2fa; _sin2fa = sin2fa;
        BaseDatos.Restablecer();
    }

    /// <summary>Crea un usuario nuevo (sin autenticador) con rol obligado a usar 2FA.</summary>
    private async Task<string> UsuarioObligado()
    {
        var admin = await Api.ComoAdmin(_sin2fa);
        var idRed = $"YPF\\F{Guid.NewGuid():N}"[..12];
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync("/api/Usuarios", SeguridadTests.NuevoUsuario(idRed))).StatusCode);
        var id = await Datos.IdUsuario(admin, idRed);
        var rol = await Datos.IdRol(admin, "Administrador de Recepción");
        Assert.True((await admin.PutAsJsonAsync($"/api/Usuarios/{id}/roles", new { roles = new[] { (int)rol } })).IsSuccessStatusCode);
        return idRed;
    }

    private async Task<string> LoginDesafio(string idRed, string clave)
    {
        var r = await _con2fa.Cliente().PostAsJsonAsync("/api/Auth/login", new { idRed, password = Api.Clave });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        using var doc = await Jx.Leer(r);
        Assert.Null(Jx.Texto(doc.RootElement, "token"));          // no hay sesión todavía
        return Jx.Texto(doc.RootElement, clave == "enrolar" ? "desafio" : "desafio")!;
    }

    /// <summary>Alta del autenticador. Devuelve el secreto y el paso de reloj ya consumido.</summary>
    private async Task<(byte[] secreto, long paso, HttpClient sesion)> Enrolar(string idRed)
    {
        var desafio = await LoginDesafio(idRed, "enrolar");
        var c = _con2fa.Cliente();
        var ini = await c.PostAsJsonAsync("/api/Auth/2fa/enrolar/iniciar", new { desafio });
        Assert.Equal(HttpStatusCode.OK, ini.StatusCode);
        using var d = await Jx.Leer(ini);
        var secreto = Totp.Base32Decodificar(Jx.Texto(d.RootElement, "secreto")!.Replace(" ", ""));

        var paso = Totp.PasoActual(DateTimeOffset.UtcNow);
        var conf = await c.PostAsJsonAsync("/api/Auth/2fa/enrolar/confirmar", new { desafio, codigo = Totp.CalcularCodigo(secreto, paso) });
        Assert.Equal(HttpStatusCode.OK, conf.StatusCode);
        using var s = await Jx.Leer(conf);
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Jx.Texto(s.RootElement, "token")!);
        return (secreto, paso, c);
    }

    [Fact]
    public async Task Rol_obligado_sin_autenticador_debe_enrolarse_y_no_recibe_sesion()
    {
        var idRed = await UsuarioObligado();
        var r = await _con2fa.Cliente().PostAsJsonAsync("/api/Auth/login", new { idRed, password = Api.Clave });
        using var doc = await Jx.Leer(r);
        Assert.True(Jx.Booleano(doc.RootElement, "requiereEnrolamiento"));
        Assert.Null(Jx.Texto(doc.RootElement, "token"));
    }

    [Fact] // SEG-15 / PS-12
    public async Task El_token_de_desafio_no_sirve_como_sesion()
    {
        var idRed = await UsuarioObligado();
        var desafio = await LoginDesafio(idRed, "enrolar");
        var c = _con2fa.Cliente();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", desafio);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/Tanques")).StatusCode);
    }

    [Fact] // SEG-09 / PS-06
    public async Task Alta_del_doble_factor_entrega_la_sesion_y_deja_el_2fa_activo()
    {
        var idRed = await UsuarioObligado();
        var (_, _, sesion) = await Enrolar(idRed);
        Assert.Equal(HttpStatusCode.OK, (await sesion.GetAsync("/api/Reportes/kpis")).StatusCode);

        var admin = await Api.ComoAdmin(_sin2fa);
        var lista = await (await admin.GetAsync("/api/Usuarios")).Content.ReadAsStringAsync();
        Assert.Contains(idRed.Replace("\\", "\\\\"), lista);
    }

    [Fact] // SEG-10 / PS-07
    public async Task Ingreso_con_contrasena_y_codigo_correcto_entrega_la_sesion()
    {
        var idRed = await UsuarioObligado();
        var (secreto, paso, _) = await Enrolar(idRed);

        var desafio = await LoginDesafio(idRed, "verificar");   // ahora pide código
        var codigo = Totp.CalcularCodigo(secreto, paso + 1);    // el siguiente paso (el actual ya se usó)
        var r = await _con2fa.Cliente().PostAsJsonAsync("/api/Auth/2fa/verificar", new { desafio, codigo });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        using var doc = await Jx.Leer(r);
        Assert.False(string.IsNullOrWhiteSpace(Jx.Texto(doc.RootElement, "token")));
    }

    [Fact] // SEG-12 / PS-09
    public async Task Un_codigo_ya_usado_se_rechaza()
    {
        var idRed = await UsuarioObligado();
        var (secreto, paso, _) = await Enrolar(idRed);
        var codigo = Totp.CalcularCodigo(secreto, paso + 1);

        var d1 = await LoginDesafio(idRed, "verificar");
        Assert.Equal(HttpStatusCode.OK, (await _con2fa.Cliente().PostAsJsonAsync("/api/Auth/2fa/verificar", new { desafio = d1, codigo })).StatusCode);

        var d2 = await LoginDesafio(idRed, "verificar");
        Assert.Equal(HttpStatusCode.Unauthorized, (await _con2fa.Cliente().PostAsJsonAsync("/api/Auth/2fa/verificar", new { desafio = d2, codigo })).StatusCode);
    }

    [Fact] // SEG-11 / PS-08
    public async Task Cinco_codigos_incorrectos_bloquean_el_ingreso()
    {
        var idRed = await UsuarioObligado();
        var (secreto, paso, _) = await Enrolar(idRed);
        var desafio = await LoginDesafio(idRed, "verificar");
        var c = _con2fa.Cliente();

        for (var i = 0; i < 5; i++)
            await c.PostAsJsonAsync("/api/Auth/2fa/verificar", new { desafio, codigo = "000000" });

        // Bloqueado: ni siquiera el código correcto entra.
        var r = await c.PostAsJsonAsync("/api/Auth/2fa/verificar", new { desafio, codigo = Totp.CalcularCodigo(secreto, paso + 1) });
        Assert.Equal(HttpStatusCode.TooManyRequests, r.StatusCode);
    }

    [Theory] // SEG-13 / PS-10: un desafío inválido o vencido pide volver a iniciar sesión
    [InlineData("")]
    [InlineData("desafio.invalido.xyz")]
    public async Task Desafio_invalido_se_rechaza(string desafio)
    {
        var r = await _con2fa.Cliente().PostAsJsonAsync("/api/Auth/2fa/verificar", new { desafio, codigo = "123456" });
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [Fact] // SEG-14 / PS-11
    public async Task El_administrador_puede_restablecer_el_doble_factor_de_otro_usuario()
    {
        var idRed = await UsuarioObligado();
        await Enrolar(idRed);

        var admin = await Api.ComoAdmin(_sin2fa);
        var id = await Datos.IdUsuario(admin, idRed);
        Assert.True((await admin.PostAsync($"/api/Usuarios/{id}/reiniciar-2fa", null)).IsSuccessStatusCode);

        var r = await _con2fa.Cliente().PostAsJsonAsync("/api/Auth/login", new { idRed, password = Api.Clave });
        using var doc = await Jx.Leer(r);
        Assert.True(Jx.Booleano(doc.RootElement, "requiereEnrolamiento"));
    }

    [Fact]
    public async Task Quien_no_es_administrador_no_puede_restablecer_el_doble_factor()
    {
        var operador = await Api.ComoOperador(_sin2fa);
        Assert.Equal(HttpStatusCode.Forbidden, (await operador.PostAsync("/api/Usuarios/1/reiniciar-2fa", null)).StatusCode);
    }
}
