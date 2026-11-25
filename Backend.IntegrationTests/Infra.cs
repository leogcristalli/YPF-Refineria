using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Backend.IntegrationTests;

/// <summary>Todas las clases comparten la misma base: se ejecutan de a una.</summary>
[CollectionDefinition(Nombre, DisableParallelization = true)]
public class ColeccionApi { public const string Nombre = "Api"; }

/// <summary>API real (Program) contra la base de demostración. Doble factor apagado.</summary>
public class CilpFactory : WebApplicationFactory<Program>
{
    public const string ClaveJwt = "clave-de-pruebas-solo-para-tests-0123456789";

    public static string Conexion => Environment.GetEnvironmentVariable("CILP_TEST_CONNECTION")
        ?? throw new InvalidOperationException(
            "Defina CILP_TEST_CONNECTION con la cadena de conexión de la base de DEMOSTRACIÓN (se la restablece en cada prueba).");

    protected virtual bool DobleFactor => false;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:DefaultConnection", Conexion);
        builder.UseSetting("Jwt:Key", ClaveJwt);
        builder.UseSetting("Auth:DobleFactor:Habilitado", DobleFactor ? "true" : "false");
    }

    public HttpClient Cliente() => CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
}

/// <summary>Igual que la anterior pero con el doble factor habilitado (como en producción).</summary>
public class CilpFactoryDobleFactor : CilpFactory
{
    protected override bool DobleFactor => true;
}

public static class BaseDatos
{
    /// <summary>Ejecuta reset_demo.sql: deja los datos de demostración en su estado inicial.</summary>
    public static void Restablecer()
    {
        var raiz = BuscarRaizRepo();
        var script = Path.Combine(raiz, "Database", "Balanza.CILP.DataBase", "scripts", "testdata", "reset_demo.sql");
        using var cn = new SqlConnection(CilpFactory.Conexion);
        cn.Open();
        EjecutarScript(cn, raiz, script);
    }

    private static void EjecutarScript(SqlConnection cn, string raiz, string rutaScript)
    {
        var sql = File.ReadAllText(rutaScript);
        var lotes = Regex.Split(sql, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)
                         .Where(l => !string.IsNullOrWhiteSpace(l));
        foreach (var lote in lotes)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var raw in lote.Split('\n'))
            {
                var linea = raw.TrimEnd('\r');
                var match = Regex.Match(linea, @"^\s*:r\s+(.+?)\s*$", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    if (!string.IsNullOrWhiteSpace(sb.ToString()))
                    {
                        EjecutarLote(cn, sb.ToString());
                        sb.Clear();
                    }
                    var incluido = Path.Combine(raiz, match.Groups[1].Value.Trim('"'));
                    EjecutarScript(cn, raiz, incluido);
                }
                else
                {
                    sb.AppendLine(linea);
                }
            }
            if (!string.IsNullOrWhiteSpace(sb.ToString()))
                EjecutarLote(cn, sb.ToString());
        }
    }

    private static void EjecutarLote(SqlConnection cn, string lote)
    {
        using var cmd = new SqlCommand(lote, cn) { CommandTimeout = 120 };
        cmd.ExecuteNonQuery();
    }

    public static T? Escalar<T>(string sql)
    {
        using var cn = new SqlConnection(CilpFactory.Conexion);
        cn.Open();
        using var cmd = new SqlCommand(sql, cn);
        var v = cmd.ExecuteScalar();
        return v == null || v is DBNull ? default : (T)Convert.ChangeType(v, typeof(T));
    }

    /// <summary>Valores de texto de la última fila de una tabla (para revisar columnas de auditoría).</summary>
    public static List<string> TextosUltimaFila(string tabla, string columnaId)
    {
        var textos = new List<string>();
        using var cn = new SqlConnection(CilpFactory.Conexion);
        cn.Open();
        using var cmd = new SqlCommand($"SELECT TOP 1 * FROM {tabla} ORDER BY {columnaId} DESC", cn);
        using var rd = cmd.ExecuteReader();
        if (rd.Read())
            for (var i = 0; i < rd.FieldCount; i++)
                if (!rd.IsDBNull(i) && rd.GetValue(i) is string s) textos.Add(s);
        return textos;
    }

    private static string BuscarRaizRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var p = Path.Combine(dir.FullName, "Database", "Balanza.CILP.DataBase", "scripts", "testdata", "reset_demo.sql");
            if (File.Exists(p)) return dir.FullName;
            dir = dir.Parent;
        }
        throw new FileNotFoundException("No se encontró Database/.../testdata/reset_demo.sql subiendo desde " + AppContext.BaseDirectory);
    }
}

/// <summary>Lectura tolerante de JSON: busca propiedades sin distinguir mayúsculas y a cualquier profundidad.</summary>
public static class Jx
{
    public static async Task<JsonDocument> Leer(HttpResponseMessage r) => JsonDocument.Parse(await r.Content.ReadAsStringAsync());

    public static JsonElement? Buscar(JsonElement e, string nombre)
    {
        if (e.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in e.EnumerateObject())
                if (string.Equals(p.Name, nombre, StringComparison.OrdinalIgnoreCase)) return p.Value;
            foreach (var p in e.EnumerateObject())
            {
                var r = Buscar(p.Value, nombre);
                if (r != null) return r;
            }
        }
        else if (e.ValueKind == JsonValueKind.Array)
        {
            foreach (var i in e.EnumerateArray())
            {
                var r = Buscar(i, nombre);
                if (r != null) return r;
            }
        }
        return null;
    }

    public static long? Entero(JsonElement e, string n) =>
        Buscar(e, n) is { ValueKind: JsonValueKind.Number } v && v.TryGetInt64(out var l) ? l : null;

    public static decimal? Decimal(JsonElement e, string n) =>
        Buscar(e, n) is { ValueKind: JsonValueKind.Number } v ? v.GetDecimal() : null;

    public static string? Texto(JsonElement e, string n) =>
        Buscar(e, n) is { ValueKind: JsonValueKind.String } v ? v.GetString() : null;

    public static bool? Booleano(JsonElement e, string n) =>
        Buscar(e, n) is { } v && (v.ValueKind == JsonValueKind.True || v.ValueKind == JsonValueKind.False) ? v.GetBoolean() : null;

    public static double SumaNumeros(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.Number => e.GetDouble(),
        JsonValueKind.Object => e.EnumerateObject().Sum(p => SumaNumeros(p.Value)),
        JsonValueKind.Array => e.EnumerateArray().Sum(SumaNumeros),
        _ => 0
    };
}

public static class Api
{
    public const string Admin = "YPF\\ADMIN";
    public const string Operador = "YPF\\OPERADOR";
    public const string Clave = "Demo1234!";

    public static async Task<HttpClient> Sesion(CilpFactory f, string idRed, string password = Clave)
    {
        var c = f.Cliente();
        var r = await c.PostAsJsonAsync("/api/Auth/login", new { idRed, password });
        r.EnsureSuccessStatusCode();
        using var doc = await Jx.Leer(r);
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Jx.Texto(doc.RootElement, "token")!);
        return c;
    }

    public static Task<HttpClient> ComoAdmin(CilpFactory f) => Sesion(f, Admin);
    public static Task<HttpClient> ComoOperador(CilpFactory f) => Sesion(f, Operador);

    public static Task<HttpResponseMessage> Patch(HttpClient c, string url, object cuerpo) =>
        c.PatchAsync(url, JsonContent.Create(cuerpo));

    public static bool Es4xx(HttpResponseMessage r) => (int)r.StatusCode is >= 400 and < 500;
}

/// <summary>Búsquedas de datos de demostración por su contenido (los IDs no se asumen).</summary>
public static class Datos
{
    public const byte Tara = 1, Bruto = 2;

    public static async Task<long> IdPor(HttpClient c, string url, string textoBuscado, string campoId)
    {
        var r = await c.GetAsync(url);
        r.EnsureSuccessStatusCode();
        using var doc = await Jx.Leer(r);
        foreach (var it in doc.RootElement.EnumerateArray())
            if (it.GetRawText().Contains(textoBuscado, StringComparison.OrdinalIgnoreCase)
                && Jx.Entero(it, campoId) is { } id) return id;
        throw new InvalidOperationException($"No se encontró '{textoBuscado}' en {url}. ¿Se cargó seed_demo.sql?");
    }

    public static Task<long> PlanificacionPendiente(HttpClient c, string patente) =>
        IdPor(c, "/api/Planificaciones/pendientes", patente, "id_Planificacion");

    public static Task<long> IdBascula(HttpClient c, string serie) => IdPor(c, "/api/Basculas", serie, "id_Bascula");

    public static Task<long> IdRol(HttpClient c, string descripcion) => IdPor(c, "/api/Roles", descripcion, "id_Rol");

    public static Task<long> IdUsuario(HttpClient c, string idRed) =>
        IdPor(c, "/api/Usuarios", idRed.Replace("\\", "\\\\"), "id_Usuario");

    public static Task<HttpResponseMessage> Pesar(HttpClient c, long idPlan, byte tipo, decimal kg,
        string? motivo = "Prueba automatizada", int? bascula = null) =>
        c.PostAsJsonAsync("/api/Pesadas", new
        {
            id_Planificacion = idPlan,
            id_TipoPesada = tipo,
            cantidad = kg,
            esIngresoManual = bascula == null,
            motivoIngresoManual = bascula == null ? motivo : null,
            id_Bascula = bascula
        });

    public static async Task<decimal> VolumenOcupado(HttpClient c, string codigoTanque)
    {
        var id = await IdPor(c, "/api/Tanques", codigoTanque, "id_Tanque");
        var r = await c.GetAsync($"/api/Tanques/{id}");
        r.EnsureSuccessStatusCode();
        using var doc = await Jx.Leer(r);
        return Jx.Decimal(doc.RootElement, "volumenOcupado")!.Value;
    }

    /// <summary>Alta de una Autorización de Descarga de crudo (camión AF789HJ, tanque TK-101).</summary>
    public static async Task<HttpResponseMessage> AltaDescarga(HttpClient c, decimal cantidad, long? remito = null)
    {
        var dto = new
        {
            id_Producto = await IdPor(c, "/api/Productos", "Petróleo Crudo", "id_Producto"),
            id_RazonSocialCliente = await IdPor(c, "/api/RazonesSociales", "Petrolera Cuenca", "id_RazonSocial"),
            id_RazonSocialTransportista = await IdPor(c, "/api/RazonesSociales", "Transportes Patag", "id_RazonSocial"),
            id_Vehiculo = await IdPor(c, "/api/Vehiculos", "AF789HJ", "id_Vehiculo"),
            id_Conductor = await IdPor(c, "/api/Conductores", "31789456", "id_Conductor"),
            id_PuestoExpedicionRecepcion = await IdPor(c, "/api/Puestos", "Acceso Norte", "id_PuestoExpedicionRecepcion"),
            nroRemito = remito ?? 9_000_000_000L + Random.Shared.Next(1, 999_999),
            cantidad,
            fecha = DateTime.Today.AddDays(2),
            id_TipoOrden = (byte)2,
            id_Tanque = await IdPor(c, "/api/Tanques", "TK-101", "id_Tanque")
        };
        return await c.PostAsJsonAsync("/api/OrdenesTransporte", dto);
    }

    public static async Task<long> IdDe(HttpResponseMessage creada)
    {
        using var doc = await Jx.Leer(creada);
        return Jx.Entero(doc.RootElement, "id_OrdenTransporte")!.Value;
    }
}
