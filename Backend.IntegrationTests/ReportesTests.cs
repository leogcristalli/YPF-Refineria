using System.Net;
using Xunit;

namespace Backend.IntegrationTests;

/// <summary>Batería "Integración de módulos": reportes CU080 a CU083 y KPIs del Panel de Control.</summary>
[Collection(ColeccionApi.Nombre)]
public class ReportesTests : IClassFixture<CilpFactory>
{
    private readonly CilpFactory _f;
    public ReportesTests(CilpFactory f) { _f = f; BaseDatos.Restablecer(); }

    private static string Rango(DateTime desde, DateTime hasta) => $"desde={desde:yyyy-MM-dd}&hasta={hasta:yyyy-MM-dd}";

    [Fact]
    public async Task Movimientos_por_periodo_exige_ambas_fechas()
    {
        var c = await Api.ComoOperador(_f);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync("/api/Reportes/movimientos-por-periodo")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync($"/api/Reportes/movimientos-por-periodo?desde={DateTime.Today:yyyy-MM-dd}")).StatusCode);
    }

    [Fact] // PR-01
    public async Task Movimientos_por_periodo_rechaza_Desde_posterior_a_Hasta()
    {
        var c = await Api.ComoOperador(_f);
        var r = await c.GetAsync("/api/Reportes/movimientos-por-periodo?" + Rango(DateTime.Today, DateTime.Today.AddDays(-5)));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact] // PR-02
    public async Task Movimientos_por_periodo_incluye_las_ordenes_de_demostracion()
    {
        var c = await Api.ComoOperador(_f);
        var r = await c.GetAsync("/api/Reportes/movimientos-por-periodo?" + Rango(DateTime.Today.AddDays(-30), DateTime.Today));
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        using var doc = await Jx.Leer(r);
        Assert.True(Jx.SumaNumeros(doc.RootElement) > 0);
    }

    [Fact] // defecto 5: sin movimientos, los reportes devuelven ceros y no valores de ejemplo
    [Trait("Defecto", "5")]
    public async Task Un_periodo_sin_movimientos_devuelve_ceros()
    {
        var c = await Api.ComoOperador(_f);
        var r = await c.GetAsync("/api/Reportes/movimientos-por-periodo?" + Rango(new DateTime(2000, 1, 1), new DateTime(2000, 1, 31)));
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        using var doc = await Jx.Leer(r);
        Assert.Equal(0d, Jx.SumaNumeros(doc.RootElement));
    }

    [Fact]
    public async Task Movimientos_por_periodo_se_puede_filtrar_por_tipo_de_orden_y_replanificacion()
    {
        var c = await Api.ComoOperador(_f);
        var rango = Rango(DateTime.Today.AddDays(-30), DateTime.Today);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync($"/api/Reportes/movimientos-por-periodo?{rango}&idTipoOrden=1")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync($"/api/Reportes/movimientos-por-periodo?{rango}&replanificado=true")).StatusCode);
    }

    [Theory] // CU081 / CU082 / CU083 y panel de control: responden sin error de servidor
    [InlineData("/api/Reportes/kpis")]
    [InlineData("/api/Reportes/ocupacion-basculas")]
    [InlineData("/api/Reportes/duracion-ordenes")]
    [InlineData("/api/Reportes/movimientos-por-producto")]
    public async Task Los_reportes_responden_sin_error_de_servidor(string url)
    {
        var c = await Api.ComoOperador(_f);
        var rango = Rango(DateTime.Today.AddDays(-30), DateTime.Today);
        var r = await c.GetAsync(url.EndsWith("kpis") ? url : $"{url}?{rango}");
        Assert.True((int)r.StatusCode < 500, $"{url} respondió {(int)r.StatusCode}");
    }

    [Fact] // PR-03
    public async Task El_panel_de_control_refleja_una_pesada_registrada()
    {
        var c = await Api.ComoOperador(_f);
        var antes = await (await c.GetAsync("/api/Reportes/kpis")).Content.ReadAsStringAsync();
        var plan = await Datos.PlanificacionPendiente(c, "AE123BC");
        await Datos.Pesar(c, plan, Datos.Tara, 15_200m);
        var despues = await (await c.GetAsync("/api/Reportes/kpis")).Content.ReadAsStringAsync();
        Assert.NotEqual(antes, despues);
    }

    [Fact]
    public async Task Los_reportes_exigen_sesion()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _f.Cliente().GetAsync("/api/Reportes/kpis")).StatusCode);
    }
}
