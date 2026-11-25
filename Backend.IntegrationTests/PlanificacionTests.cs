using System.Net;
using System.Net.Http.Json;
using Xunit;
using static Backend.IntegrationTests.Datos;

namespace Backend.IntegrationTests;

/// <summary>Batería "Planificación logística": altas, capacidad de tanques, reservas, replanificación y cancelación.</summary>
[Collection(ColeccionApi.Nombre)]
public class PlanificacionTests : IClassFixture<CilpFactory>
{
    private readonly CilpFactory _f;
    public PlanificacionTests(CilpFactory f) { _f = f; BaseDatos.Restablecer(); }

    [Fact] // PLA-01
    public async Task Alta_de_una_descarga_reserva_volumen_en_el_tanque()
    {
        var c = await Api.ComoAdmin(_f);
        var antes = await VolumenOcupado(c, "TK-101");
        var r = await AltaDescarga(c, 1_000m);
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        Assert.True(await VolumenOcupado(c, "TK-101") > antes);
    }

    [Fact] // PL-01
    public async Task Una_descarga_que_no_entra_en_el_tanque_se_rechaza()
    {
        var c = await Api.ComoAdmin(_f);
        Assert.Equal(HttpStatusCode.BadRequest, (await AltaDescarga(c, 100_000_000m)).StatusCode);
    }

    [Fact]
    public async Task Una_descarga_sin_cantidad_se_rechaza()
    {
        var c = await Api.ComoAdmin(_f);
        Assert.Equal(HttpStatusCode.BadRequest, (await AltaDescarga(c, 0m)).StatusCode);
    }

    [Fact]
    public async Task No_se_repite_el_numero_de_remito()
    {
        var c = await Api.ComoAdmin(_f);
        Assert.Equal(HttpStatusCode.Created, (await AltaDescarga(c, 1_000m, remito: 9_100_000_001)).StatusCode);
        Assert.True(Api.Es4xx(await AltaDescarga(c, 1_000m, remito: 9_100_000_001)));
    }

    [Fact]
    public async Task La_fecha_planificada_no_puede_ser_anterior_a_hoy()
    {
        var c = await Api.ComoAdmin(_f);
        var ok = await AltaDescarga(c, 1_000m);
        Assert.Equal(HttpStatusCode.Created, ok.StatusCode);
        var id = await IdDe(ok);
        Assert.Equal(HttpStatusCode.BadRequest, (await Api.Patch(c, $"/api/OrdenesTransporte/{id}/replanificar",
            new { fecha = DateTime.Today.AddDays(-1), motivo = "Prueba" })).StatusCode);
    }

    // ───────────── Cancelación ─────────────
    [Fact] // PL-02
    public async Task Cancelar_una_descarga_abierta_exige_motivo_y_libera_el_volumen()
    {
        var c = await Api.ComoAdmin(_f);
        var base0 = await VolumenOcupado(c, "TK-101");
        var id = await IdDe(await AltaDescarga(c, 1_000m));

        Assert.Equal(HttpStatusCode.BadRequest, (await Api.Patch(c, $"/api/OrdenesTransporte/{id}/cancelar", new { motivo = "" })).StatusCode);
        Assert.True((await Api.Patch(c, $"/api/OrdenesTransporte/{id}/cancelar", new { motivo = "Prueba automatizada" })).IsSuccessStatusCode);
        Assert.Equal(base0, await VolumenOcupado(c, "TK-101"));
    }

    [Fact]
    public async Task No_se_cancela_dos_veces_la_misma_orden()
    {
        var c = await Api.ComoAdmin(_f);
        var id = await IdDe(await AltaDescarga(c, 1_000m));
        await Api.Patch(c, $"/api/OrdenesTransporte/{id}/cancelar", new { motivo = "Primera" });
        Assert.Equal(HttpStatusCode.BadRequest, (await Api.Patch(c, $"/api/OrdenesTransporte/{id}/cancelar", new { motivo = "Segunda" })).StatusCode);
    }

    [Fact]
    public async Task No_se_cancela_una_orden_ya_cerrada_con_sus_pesadas()
    {
        var c = await Api.ComoAdmin(_f);
        var idCerrada = await IdPor(c, "/api/OrdenesTransporte", "2026001", "id_OrdenTransporte");
        Assert.Equal(HttpStatusCode.BadRequest, (await Api.Patch(c, $"/api/OrdenesTransporte/{idCerrada}/cancelar", new { motivo = "Prueba" })).StatusCode);
    }

    [Fact]
    public async Task El_motivo_de_cancelacion_tiene_largo_maximo()
    {
        var c = await Api.ComoAdmin(_f);
        var id = await IdDe(await AltaDescarga(c, 1_000m));
        Assert.Equal(HttpStatusCode.BadRequest, (await Api.Patch(c, $"/api/OrdenesTransporte/{id}/cancelar", new { motivo = new string('x', 5_001) })).StatusCode);
    }

    // ───────────── Replanificación ─────────────
    [Fact] // PLA-03
    public async Task Replanificar_con_fecha_y_motivo_se_acepta()
    {
        var c = await Api.ComoAdmin(_f);
        var id = await IdDe(await AltaDescarga(c, 1_000m));
        var r = await Api.Patch(c, $"/api/OrdenesTransporte/{id}/replanificar", new { fecha = DateTime.Today.AddDays(7), motivo = "Camión demorado" });
        Assert.True(r.IsSuccessStatusCode, await r.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Replanificar_sin_motivo_se_rechaza()
    {
        var c = await Api.ComoAdmin(_f);
        var id = await IdDe(await AltaDescarga(c, 1_000m));
        Assert.Equal(HttpStatusCode.BadRequest, (await Api.Patch(c, $"/api/OrdenesTransporte/{id}/replanificar",
            new { fecha = DateTime.Today.AddDays(7), motivo = "" })).StatusCode);
    }

    [Fact]
    public async Task Replanificar_sin_fecha_se_rechaza()
    {
        var c = await Api.ComoAdmin(_f);
        var id = await IdDe(await AltaDescarga(c, 1_000m));
        Assert.Equal(HttpStatusCode.BadRequest, (await Api.Patch(c, $"/api/OrdenesTransporte/{id}/replanificar", new { motivo = "Sin fecha" })).StatusCode);
    }

    [Fact]
    public async Task Replanificar_a_la_misma_fecha_se_rechaza()
    {
        var c = await Api.ComoAdmin(_f);
        var id = await IdDe(await AltaDescarga(c, 1_000m));   // el alta usa hoy + 2 días
        Assert.Equal(HttpStatusCode.BadRequest, (await Api.Patch(c, $"/api/OrdenesTransporte/{id}/replanificar",
            new { fecha = DateTime.Today.AddDays(2), motivo = "Igual" })).StatusCode);
    }

    // ───────────── Modificación y borrado ─────────────
    [Fact] // PLA-02: modificar una orden abierta recalcula la reserva del tanque
    public async Task Modificar_la_cantidad_de_una_orden_abierta_recalcula_la_reserva()
    {
        var c = await Api.ComoAdmin(_f);
        var remito = 9_200_000_001;
        var id = await IdDe(await AltaDescarga(c, 1_000m, remito));
        var conUno = await VolumenOcupado(c, "TK-101");

        var dto = await DtoDescarga(c, 2_000m, remito);
        var r = await c.PutAsJsonAsync($"/api/OrdenesTransporte/{id}", dto);
        Assert.True(r.IsSuccessStatusCode, await r.Content.ReadAsStringAsync());
        Assert.True(await VolumenOcupado(c, "TK-101") > conUno);
    }

    [Fact] // PL-03: una orden con pesadas no se modifica ni se borra
    public async Task Una_orden_con_pesadas_no_se_puede_borrar()
    {
        var c = await Api.ComoAdmin(_f);
        var plan = await PlanificacionPendiente(c, "AE123BC");
        await Pesar(c, plan, Tara, 15_200m);
        var idOt = await IdPor(c, "/api/OrdenesTransporte", "2026008", "id_OrdenTransporte");
        Assert.True(Api.Es4xx(await c.DeleteAsync($"/api/OrdenesTransporte/{idOt}")));
    }

    [Fact]
    public async Task Una_orden_sin_pesadas_se_puede_borrar_y_libera_el_volumen()
    {
        var c = await Api.ComoAdmin(_f);
        var base0 = await VolumenOcupado(c, "TK-101");
        var id = await IdDe(await AltaDescarga(c, 1_000m));
        Assert.True((await c.DeleteAsync($"/api/OrdenesTransporte/{id}")).IsSuccessStatusCode);
        Assert.Equal(base0, await VolumenOcupado(c, "TK-101"));
    }

    // ───────────── Defecto 7 ─────────────
    [Fact] // el restablecimiento no debe devolver dos veces el volumen de órdenes anuladas
    [Trait("Defecto", "7")]
    public async Task Restablecer_los_datos_no_duplica_el_volumen_de_ordenes_anuladas()
    {
        var c = await Api.ComoAdmin(_f);
        var base0 = BaseDatos.Escalar<decimal>("SELECT VolumenOcupado_Tanque FROM dbo.t_Tanque WHERE Codigo_Tanque = 'TK-101'");
        var id = await IdDe(await AltaDescarga(c, 1_000m));
        await Api.Patch(c, $"/api/OrdenesTransporte/{id}/cancelar", new { motivo = "Prueba" });

        BaseDatos.Restablecer();
        Assert.Equal(base0, BaseDatos.Escalar<decimal>("SELECT VolumenOcupado_Tanque FROM dbo.t_Tanque WHERE Codigo_Tanque = 'TK-101'"));
    }

    private static async Task<object> DtoDescarga(HttpClient c, decimal cantidad, long remito) => new
    {
        id_Producto = await IdPor(c, "/api/Productos", "Petróleo Crudo", "id_Producto"),
        id_RazonSocialCliente = await IdPor(c, "/api/RazonesSociales", "Petrolera Cuenca", "id_RazonSocial"),
        id_RazonSocialTransportista = await IdPor(c, "/api/RazonesSociales", "Transportes Patag", "id_RazonSocial"),
        id_Vehiculo = await IdPor(c, "/api/Vehiculos", "AF789HJ", "id_Vehiculo"),
        id_Conductor = await IdPor(c, "/api/Conductores", "31789456", "id_Conductor"),
        id_PuestoExpedicionRecepcion = await IdPor(c, "/api/Puestos", "Acceso Norte", "id_PuestoExpedicionRecepcion"),
        nroRemito = remito, cantidad, fecha = DateTime.Today.AddDays(2), id_TipoOrden = (byte)2,
        id_Tanque = await IdPor(c, "/api/Tanques", "TK-101", "id_Tanque")
    };
}
