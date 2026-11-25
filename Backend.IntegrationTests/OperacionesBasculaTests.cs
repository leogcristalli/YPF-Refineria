using System.Net;
using System.Net.Http.Json;
using Xunit;
using static Backend.IntegrationTests.Datos;

namespace Backend.IntegrationTests;

/// <summary>
/// Batería "Operaciones de báscula". Datos de demostración usados:
///   OT 2026008: Entrega de Nafta Súper (30.000 L x 0,74 = 22.200 kg), camión AE123BC (tara 15.200 kg),
///   tolerancia del producto 0,5 % (111 kg). Neto = Bruto - Tara.
/// </summary>
[Collection(ColeccionApi.Nombre)]
public class OperacionesBasculaTests : IClassFixture<CilpFactory>
{
    private const string Patente = "AE123BC";
    private const decimal TaraKg = 15_200m, BrutoOkKg = 37_400m;
    private readonly CilpFactory _f;

    public OperacionesBasculaTests(CilpFactory f) { _f = f; BaseDatos.Restablecer(); }

    // ───────────── Búsqueda por patente ─────────────
    [Fact] // PES-01 / defecto 1: devuelve la orden pendiente, no una ya cerrada
    [Trait("Defecto", "1")]
    public async Task Buscar_por_patente_devuelve_la_orden_pendiente_y_no_una_cerrada()
    {
        var c = await Api.ComoOperador(_f);
        var r = await c.GetAsync($"/api/OrdenesTransporte/ByPatente/{Patente}");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var json = await r.Content.ReadAsStringAsync();
        Assert.Contains("2026008", json);
        Assert.DoesNotContain("2026005", json);
        Assert.DoesNotContain("2026001", json);
    }

    [Fact]
    public async Task Buscar_por_patente_desconocida_responde_404()
    {
        var c = await Api.ComoOperador(_f);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync("/api/OrdenesTransporte/ByPatente/ZZ999ZZ")).StatusCode);
    }

    [Fact] // PES-04
    public async Task Despues_del_cierre_el_camion_no_tiene_ordenes_pendientes()
    {
        var c = await Api.ComoOperador(_f);
        var plan = await PlanificacionPendiente(c, Patente);
        Assert.Equal(HttpStatusCode.Created, (await Pesar(c, plan, Tara, TaraKg)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await Pesar(c, plan, Bruto, BrutoOkKg)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/api/OrdenesTransporte/ByPatente/{Patente}")).StatusCode);
    }

    [Fact]
    public async Task Buscar_por_patente_ignora_espacios()
    {
        var c = await Api.ComoOperador(_f);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync($"/api/OrdenesTransporte/ByPatente/%20{Patente}%20")).StatusCode);
    }

    // ───────────── Validaciones de la pesada ─────────────
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task El_peso_debe_ser_mayor_a_cero(int kg)
    {
        var c = await Api.ComoOperador(_f);
        var plan = await PlanificacionPendiente(c, Patente);
        Assert.Equal(HttpStatusCode.BadRequest, (await Pesar(c, plan, Tara, kg)).StatusCode);
    }

    [Fact] // PES-06 / PF-07
    public async Task Ingreso_manual_sin_motivo_se_rechaza()
    {
        var c = await Api.ComoOperador(_f);
        var plan = await PlanificacionPendiente(c, Patente);
        Assert.Equal(HttpStatusCode.BadRequest, (await Pesar(c, plan, Tara, TaraKg, motivo: "")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Pesar(c, plan, Tara, TaraKg, motivo: "   ")).StatusCode);
    }

    [Fact]
    public async Task El_motivo_del_ingreso_manual_admite_hasta_300_caracteres()
    {
        var c = await Api.ComoOperador(_f);
        var plan = await PlanificacionPendiente(c, Patente);
        Assert.Equal(HttpStatusCode.BadRequest, (await Pesar(c, plan, Tara, TaraKg, motivo: new string('x', 301))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await Pesar(c, plan, Tara, TaraKg, motivo: new string('x', 300))).StatusCode);
    }

    [Fact]
    public async Task Pesada_automatica_sin_bascula_se_rechaza()
    {
        var c = await Api.ComoOperador(_f);
        var plan = await PlanificacionPendiente(c, Patente);
        var r = await c.PostAsJsonAsync("/api/Pesadas", new { id_Planificacion = plan, id_TipoPesada = Tara, cantidad = TaraKg, esIngresoManual = false });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact] // báscula inválida
    public async Task Pesada_con_bascula_inexistente_se_rechaza()
    {
        var c = await Api.ComoOperador(_f);
        var plan = await PlanificacionPendiente(c, Patente);
        Assert.Equal(HttpStatusCode.BadRequest, (await Pesar(c, plan, Tara, TaraKg, bascula: 99_999)).StatusCode);
    }

    [Fact]
    public async Task Pesada_sobre_una_planificacion_inexistente_se_rechaza()
    {
        var c = await Api.ComoOperador(_f);
        Assert.Equal(HttpStatusCode.BadRequest, (await Pesar(c, 999_999_999, Tara, TaraKg)).StatusCode);
    }

    // ───────────── Pesada automática ─────────────
    [Fact] // PES-02
    public async Task Primera_pesada_automatica_con_la_lectura_de_la_bascula_se_registra()
    {
        var c = await Api.ComoOperador(_f);
        var plan = await PlanificacionPendiente(c, Patente);
        var bascula = await IdBascula(c, "BC80-001234");

        var peso = await LeerBascula(c, bascula, plan);
        var r = await c.PostAsJsonAsync("/api/Pesadas",
            new { id_Planificacion = plan, id_TipoPesada = Tara, cantidad = peso, esIngresoManual = false, id_Bascula = bascula });
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
    }

    [Fact] // PES-03: la segunda pesada calcula el neto y cierra la orden
    public async Task Segunda_pesada_automatica_cierra_la_orden()
    {
        var c = await Api.ComoOperador(_f);
        var plan = await PlanificacionPendiente(c, Patente);
        var bascula = await IdBascula(c, "BC80-001234");

        var tara = await LeerBascula(c, bascula, plan);
        Assert.Equal(HttpStatusCode.Created, (await c.PostAsJsonAsync("/api/Pesadas",
            new { id_Planificacion = plan, id_TipoPesada = Tara, cantidad = tara, esIngresoManual = false, id_Bascula = bascula })).StatusCode);

        var bruto = await LeerBascula(c, bascula, plan);
        Assert.Equal(HttpStatusCode.Created, (await c.PostAsJsonAsync("/api/Pesadas",
            new { id_Planificacion = plan, id_TipoPesada = Bruto, cantidad = bruto, esIngresoManual = false, id_Bascula = bascula })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/api/OrdenesTransporte/ByPatente/{Patente}")).StatusCode);
    }

    [Fact] // PF-06 / defecto 2: automática con el peso escrito a mano
    [Trait("Defecto", "2")]
    public async Task Pesada_marcada_como_automatica_con_un_peso_distinto_de_la_lectura_se_rechaza()
    {
        var c = await Api.ComoOperador(_f);
        var plan = await PlanificacionPendiente(c, Patente);
        var bascula = await IdBascula(c, "BC80-001234");
        var lectura = await LeerBascula(c, bascula, plan);

        var r = await c.PostAsJsonAsync("/api/Pesadas",
            new { id_Planificacion = plan, id_TipoPesada = Tara, cantidad = lectura + 500m, esIngresoManual = false, id_Bascula = bascula });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task Pesada_automatica_sin_lectura_previa_se_rechaza()
    {
        var c = await Api.ComoOperador(_f);
        var plan = await PlanificacionPendiente(c, Patente);
        var bascula = await IdBascula(c, "BC80-001234");
        var r = await c.PostAsJsonAsync("/api/Pesadas",
            new { id_Planificacion = plan, id_TipoPesada = Tara, cantidad = TaraKg, esIngresoManual = false, id_Bascula = bascula });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task Leer_una_bascula_inexistente_responde_404()
    {
        var c = await Api.ComoOperador(_f);
        var plan = await PlanificacionPendiente(c, Patente);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/api/Hardware/basculas/99999/peso?idPlanificacion={plan}")).StatusCode);
    }

    // ───────────── Tolerancia, orden de pesadas, duplicados ─────────────
    [Fact] // PES-07 / PF-03
    public async Task Pesada_fuera_de_la_tolerancia_del_producto_se_rechaza()
    {
        var c = await Api.ComoOperador(_f);
        var plan = await PlanificacionPendiente(c, Patente);
        Assert.Equal(HttpStatusCode.Created, (await Pesar(c, plan, Tara, TaraKg)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Pesar(c, plan, Bruto, TaraKg + 22_800m)).StatusCode);   // desvío 2,7 %
    }

    [Fact]
    public async Task Pesada_dentro_de_la_tolerancia_se_acepta()
    {
        var c = await Api.ComoOperador(_f);
        var plan = await PlanificacionPendiente(c, Patente);
        Assert.Equal(HttpStatusCode.Created, (await Pesar(c, plan, Tara, TaraKg)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await Pesar(c, plan, Bruto, TaraKg + 22_200m + 100m)).StatusCode);   // 100 kg < 111 kg
    }

    [Fact] // PF-04
    public async Task El_bruto_debe_ser_mayor_que_la_tara()
    {
        var c = await Api.ComoOperador(_f);
        var plan = await PlanificacionPendiente(c, Patente);
        Assert.Equal(HttpStatusCode.Created, (await Pesar(c, plan, Tara, TaraKg)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Pesar(c, plan, Bruto, TaraKg - 1_000m)).StatusCode);
    }

    [Fact] // tercera pesada / PF-05
    public async Task Una_orden_con_sus_dos_pesadas_no_admite_una_tercera()
    {
        var c = await Api.ComoOperador(_f);
        var plan = await PlanificacionPendiente(c, Patente);
        await Pesar(c, plan, Tara, TaraKg);
        await Pesar(c, plan, Bruto, BrutoOkKg);
        Assert.Equal(HttpStatusCode.BadRequest, (await Pesar(c, plan, Tara, TaraKg)).StatusCode);
    }

    [Fact] // duplicados
    public async Task No_se_registra_dos_veces_la_misma_pesada()
    {
        var c = await Api.ComoOperador(_f);
        var plan = await PlanificacionPendiente(c, Patente);
        Assert.Equal(HttpStatusCode.Created, (await Pesar(c, plan, Tara, TaraKg)).StatusCode);
        Assert.True(Api.Es4xx(await Pesar(c, plan, Tara, TaraKg)));
    }

    [Fact] // defecto 3: dos registros simultáneos no duplican la pesada
    [Trait("Defecto", "3")]
    public async Task Dos_registros_simultaneos_de_la_misma_pesada_graban_una_sola()
    {
        var c1 = await Api.ComoOperador(_f);
        var c2 = await Api.ComoOperador(_f);
        var plan = await PlanificacionPendiente(c1, Patente);

        var rs = await Task.WhenAll(Pesar(c1, plan, Tara, TaraKg), Pesar(c2, plan, Tara, TaraKg));
        Assert.Single(rs, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Equal(1, BaseDatos.Escalar<int>($"SELECT COUNT(*) FROM dbo.t_Pesada WHERE Id_Planificacion = {plan} AND Id_TipoPesada = 1"));
    }

    // ───────────── Ticket ─────────────
    [Fact] // PES-05 / PF-08
    public async Task El_ticket_de_una_orden_completa_muestra_las_dos_pesadas()
    {
        var c = await Api.ComoOperador(_f);
        var plan = await PlanificacionPendiente(c, Patente);
        await Pesar(c, plan, Tara, TaraKg);
        await Pesar(c, plan, Bruto, BrutoOkKg);

        var r = await c.GetAsync($"/api/Pesadas/Ticket/{plan}");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var json = await r.Content.ReadAsStringAsync();
        Assert.Contains("15200", json);
        Assert.Contains("37400", json);
    }

    [Fact]
    public async Task Las_pesadas_de_una_planificacion_se_consultan_en_orden()
    {
        var c = await Api.ComoOperador(_f);
        var plan = await PlanificacionPendiente(c, Patente);
        await Pesar(c, plan, Tara, TaraKg);
        var r = await c.GetAsync($"/api/Pesadas/ByPlanificacion/{plan}");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        using var doc = await Jx.Leer(r);
        Assert.Equal(1, doc.RootElement.GetArrayLength());
    }

    // ───────────── Cámara ─────────────
    [Fact] // PES-08
    public async Task La_deteccion_por_camara_muestra_un_camion_con_orden_pendiente()
    {
        var c = await Api.ComoOperador(_f);
        var dispositivo = await IdPor(c, "/api/DispositivosDetectores", "", "id_DispositivoDetector");
        var r = await c.PostAsync($"/api/Hardware/dispositivos/{dispositivo}/detectar", null);
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var json = await r.Content.ReadAsStringAsync();
        Assert.True(new[] { "AE123BC", "AD345MN", "AF789HJ" }.Any(json.Contains), "La detección no devolvió ninguna patente de demostración.");
    }

    [Fact]
    public async Task Detectar_con_un_dispositivo_inexistente_responde_404()
    {
        var c = await Api.ComoOperador(_f);
        Assert.Equal(HttpStatusCode.NotFound, (await c.PostAsync("/api/Hardware/dispositivos/99999/detectar", null)).StatusCode);
    }

    private static async Task<decimal> LeerBascula(HttpClient c, long bascula, long plan)
    {
        var r = await c.GetAsync($"/api/Hardware/basculas/{bascula}/peso?idPlanificacion={plan}");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        using var doc = await Jx.Leer(r);
        return Jx.Decimal(doc.RootElement, "pesoKg")!.Value;
    }
}
