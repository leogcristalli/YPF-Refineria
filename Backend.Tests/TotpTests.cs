using System.Text;
using Backend.Auth;

namespace Backend.Tests;

public class TotpTests
{
    // Secreto de los vectores de prueba del RFC 6238 (Apéndice B, SHA-1): "12345678901234567890".
    private static readonly byte[] SecretoRfc = Encoding.ASCII.GetBytes("12345678901234567890");
    private static readonly string SecretoRfcBase32 = Totp.Base32Codificar(SecretoRfc);

    // Instante 59 s => paso 1 (con pasos de 30 s). La ventana de tolerancia cubre los pasos 0, 1 y 2.
    private static readonly DateTimeOffset Ahora = DateTimeOffset.FromUnixTimeSeconds(59);

    private static string Codigo(long paso) => Totp.CalcularCodigo(SecretoRfc, paso);

    // ───────────── Algoritmo ─────────────

    // El RFC publica códigos de 8 dígitos; los de 6 dígitos son sus últimos seis.
    [Theory]
    [InlineData(59L, "287082")]
    [InlineData(1111111109L, "081804")]
    [InlineData(1111111111L, "050471")]
    [InlineData(1234567890L, "005924")]
    [InlineData(2000000000L, "279037")]
    [InlineData(20000000000L, "353130")]
    public void CalcularCodigo_coincide_con_los_vectores_del_RFC_6238(long segundos, string esperado)
    {
        Assert.Equal(esperado, Codigo(segundos / Totp.PasoSegundos));
    }

    // ───────────── Verificación ─────────────

    [Fact]
    public void Verificar_acepta_el_codigo_del_paso_actual()
    {
        Assert.True(Totp.Verificar(SecretoRfcBase32, Codigo(1), null, Ahora, out var paso));
        Assert.Equal(1L, paso);
    }

    [Fact]
    public void Verificar_acepta_el_paso_anterior_por_diferencia_de_reloj()
    {
        Assert.True(Totp.Verificar(SecretoRfcBase32, Codigo(0), null, Ahora, out var paso));
        Assert.Equal(0L, paso);
    }

    [Fact]
    public void Verificar_acepta_el_paso_siguiente_por_diferencia_de_reloj()
    {
        Assert.True(Totp.Verificar(SecretoRfcBase32, Codigo(2), null, Ahora, out var paso));
        Assert.Equal(2L, paso);
    }

    [Fact]
    public void Verificar_rechaza_un_codigo_fuera_de_la_ventana()
    {
        Assert.False(Totp.Verificar(SecretoRfcBase32, Codigo(3), null, Ahora, out _));
    }

    [Fact]
    public void Verificar_rechaza_un_codigo_incorrecto()
    {
        Assert.False(Totp.Verificar(SecretoRfcBase32, "000000", null, Ahora, out _));
    }

    [Fact]
    public void Verificar_rechaza_reutilizar_un_codigo_ya_aceptado()
    {
        // El último paso aceptado fue el 1: ese código y los anteriores ya no sirven.
        Assert.False(Totp.Verificar(SecretoRfcBase32, Codigo(1), 1L, Ahora, out _));
        Assert.False(Totp.Verificar(SecretoRfcBase32, Codigo(0), 1L, Ahora, out _));
    }

    [Fact]
    public void Verificar_acepta_un_paso_posterior_al_ultimo_usado()
    {
        Assert.True(Totp.Verificar(SecretoRfcBase32, Codigo(2), 1L, Ahora, out var paso));
        Assert.Equal(2L, paso);
    }

    [Fact]
    public void Verificar_admite_espacios_en_el_codigo()
    {
        var codigo = Codigo(1);
        var conEspacio = codigo[..3] + " " + codigo[3..];

        Assert.True(Totp.Verificar(SecretoRfcBase32, conEspacio, null, Ahora, out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("28708")]
    [InlineData("2870823")]
    [InlineData("abcdef")]
    public void Verificar_rechaza_codigos_mal_formados(string? codigo)
    {
        Assert.False(Totp.Verificar(SecretoRfcBase32, codigo, null, Ahora, out _));
    }

    // ───────────── Secreto y Base32 ─────────────

    [Fact]
    public void Base32Codificar_coincide_con_el_ejemplo_del_RFC_4648()
    {
        // RFC 4648: BASE32("foobar") = "MZXW6YTBOI======" (aquí sin relleno).
        Assert.Equal("MZXW6YTBOI", Totp.Base32Codificar(Encoding.ASCII.GetBytes("foobar")));
    }

    [Fact]
    public void Base32_codifica_y_decodifica_sin_perder_datos()
    {
        for (int i = 0; i < 100; i++)
        {
            var datos = System.Security.Cryptography.RandomNumberGenerator.GetBytes(20);
            Assert.Equal(datos, Totp.Base32Decodificar(Totp.Base32Codificar(datos)));
        }
    }

    [Fact]
    public void Base32Decodificar_rechaza_caracteres_invalidos()
    {
        Assert.Throws<FormatException>(() => Totp.Base32Decodificar("AB1!"));
    }

    [Fact]
    public void GenerarSecreto_devuelve_160_bits_en_Base32_y_no_se_repite()
    {
        var a = Totp.GenerarSecreto();
        var b = Totp.GenerarSecreto();

        Assert.Equal(32, a.Length);
        Assert.Equal(20, Totp.Base32Decodificar(a).Length);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void UriOtpAuth_arma_la_direccion_que_lee_la_aplicacion_autenticadora()
    {
        var uri = Totp.UriOtpAuth("CILP Refinería", @"YPF\ADMIN", "ABCDEFGH");

        Assert.StartsWith("otpauth://totp/CILP%20Refiner%C3%ADa:YPF%5CADMIN?", uri);
        Assert.Contains("secret=ABCDEFGH", uri);
        Assert.Contains("issuer=CILP%20Refiner%C3%ADa", uri);
        Assert.Contains("digits=6", uri);
        Assert.Contains("period=30", uri);
    }
}