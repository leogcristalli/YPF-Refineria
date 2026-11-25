using System.Net;
using System.Net.Http.Json;
using Xunit;
using static Backend.IntegrationTests.Datos;

namespace Backend.IntegrationTests;

/// <summary>Batería "Migración a SQL Server / Maestros": consulta, alta, modificación y baja; validaciones; conflictos.</summary>
[Collection(ColeccionApi.Nombre)]
public class MaestrosTests : IClassFixture<CilpFactory>
{
    private readonly CilpFactory _f;
    public MaestrosTests(CilpFactory f) { _f = f; BaseDatos.Restablecer(); }

    // ───────────── Consultas ─────────────
    [Theory]
    [InlineData("/api/Tanques")] [InlineData("/api/Vehiculos")] [InlineData("/api/Conductores")]
    [InlineData("/api/Productos")] [InlineData("/api/RazonesSociales")] [InlineData("/api/Basculas")]
    [InlineData("/api/Puestos")] [InlineData("/api/DispositivosDetectores")] [InlineData("/api/TiposDocumento")]
    public async Task Los_maestros_se_consultan_con_cualquier_usuario_logueado(string url)
    {
        var c = await Api.ComoOperador(_f);
        var r = await c.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        using var doc = await Jx.Leer(r);
        Assert.True(doc.RootElement.GetArrayLength() > 0, $"{url} no devolvió datos de demostración");
    }

    [Fact]
    public async Task Consultar_un_tanque_inexistente_responde_404()
    {
        var c = await Api.ComoAdmin(_f);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync("/api/Tanques/99999")).StatusCode);
    }

    // ───────────── Tanques ─────────────
    private static object Tanque(string codigo, decimal max = 1_000m, decimal ocupado = 0m, int id = 0) =>
        new { id_Tanque = id, codigo, descripcion = "Tanque de prueba", volumenMaximo = max, volumenOcupado = ocupado };

    [Fact]
    public async Task Alta_de_un_tanque_valido()
    {
        var c = await Api.ComoAdmin(_f);
        var codigo = $"TK-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";
        var res = await c.PostAsJsonAsync("/api/Tanques", Tanque(codigo));
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        using var doc = await Jx.Leer(res);
        if (Jx.Entero(doc.RootElement, "id_Tanque") is { } id)
            await c.DeleteAsync($"/api/Tanques/{id}");
    }

    [Fact] // PM-01
    public async Task No_se_repite_el_codigo_de_un_tanque_activo()
    {
        var c = await Api.ComoAdmin(_f);
        Assert.True(Api.Es4xx(await c.PostAsJsonAsync("/api/Tanques", Tanque("TK-101"))));
    }

    [Fact]
    public async Task El_alta_de_un_tanque_sin_codigo_se_rechaza()
    {
        var c = await Api.ComoAdmin(_f);
        Assert.True(Api.Es4xx(await c.PostAsJsonAsync("/api/Tanques", Tanque(""))));
    }

    [Fact] // PM-02
    public async Task El_volumen_maximo_no_puede_ser_menor_al_ocupado()
    {
        var c = await Api.ComoAdmin(_f);
        var id = (int)await IdPor(c, "/api/Tanques", "TK-101", "id_Tanque");
        var r = await c.PutAsJsonAsync($"/api/Tanques/{id}", Tanque("TK-101", max: 1_000m, ocupado: 3_200_000m, id: id));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task El_identificador_del_cuerpo_debe_coincidir_con_el_de_la_ruta()
    {
        var c = await Api.ComoAdmin(_f);
        var id = (int)await IdPor(c, "/api/Tanques", "TK-101", "id_Tanque");
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PutAsJsonAsync($"/api/Tanques/{id}", Tanque("TK-101", id: id + 1))).StatusCode);
    }

    [Fact] // PM-02
    public async Task No_se_da_de_baja_un_tanque_con_volumen()
    {
        var c = await Api.ComoAdmin(_f);
        var id = await IdPor(c, "/api/Tanques", "TK-101", "id_Tanque");
        Assert.Equal(HttpStatusCode.BadRequest, (await c.DeleteAsync($"/api/Tanques/{id}")).StatusCode);
    }

    [Fact]
    public async Task Un_tanque_vacio_se_da_de_baja_y_deja_de_listarse()
    {
        var c = await Api.ComoAdmin(_f);
        await c.PostAsJsonAsync("/api/Tanques", Tanque("TK-T02"));
        var id = await IdPor(c, "/api/Tanques", "TK-T02", "id_Tanque");
        Assert.True((await c.DeleteAsync($"/api/Tanques/{id}")).IsSuccessStatusCode);
        Assert.DoesNotContain("TK-T02", await (await c.GetAsync("/api/Tanques")).Content.ReadAsStringAsync());
    }

    // ───────────── Usuarios ─────────────
    [Fact] // defecto 4: el alta de usuarios fallaba por un patrón de validación inválido
    [Trait("Defecto", "4")]
    public async Task Alta_de_un_usuario_valido()
    {
        var c = await Api.ComoAdmin(_f);
        var r = await c.PostAsJsonAsync("/api/Usuarios", SeguridadTests.NuevoUsuario($"YPF\\U{Guid.NewGuid():N}"[..12]));
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
    }

    [Fact]
    public async Task No_se_repite_el_ID_de_red()
    {
        var c = await Api.ComoAdmin(_f);
        Assert.True(Api.Es4xx(await c.PostAsJsonAsync("/api/Usuarios", SeguridadTests.NuevoUsuario(Api.Operador))));
    }

    [Fact]
    public async Task El_alta_de_un_usuario_exige_contrasena()
    {
        var c = await Api.ComoAdmin(_f);
        var r = await c.PostAsJsonAsync("/api/Usuarios", new
        {
            idRed = $"YPF\\S{Guid.NewGuid():N}"[..12], nombre = "A", apellido = "B", nroDocumento = "12345678", id_TipoDocumento = (byte)1
        });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task El_alta_de_un_usuario_sin_nombre_se_rechaza()
    {
        var c = await Api.ComoAdmin(_f);
        var r = await c.PostAsJsonAsync("/api/Usuarios", new
        {
            idRed = $"YPF\\N{Guid.NewGuid():N}"[..12], nombre = "", apellido = "B", nroDocumento = "12345678", id_TipoDocumento = (byte)1, password = Api.Clave
        });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task Un_usuario_dado_de_baja_no_puede_iniciar_sesion()
    {
        var c = await Api.ComoAdmin(_f);
        var idRed = $"YPF\\B{Guid.NewGuid():N}"[..12];
        await c.PostAsJsonAsync("/api/Usuarios", SeguridadTests.NuevoUsuario(idRed));
        var id = await IdUsuario(c, idRed);
        Assert.True((await c.DeleteAsync($"/api/Usuarios/{id}")).IsSuccessStatusCode);
        var r = await _f.Cliente().PostAsJsonAsync("/api/Auth/login", new { idRed, password = Api.Clave });
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }
}
