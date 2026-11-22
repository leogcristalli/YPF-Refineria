using System.Text;
using Backend.Auth;
using Microsoft.Extensions.Configuration;

namespace Backend.Tests;

public class ProtectorTotpTests
{
    private const string ClaveA = "clave-de-prueba-A-de-al-menos-32-caracteres";
    private const string ClaveB = "clave-de-prueba-B-de-al-menos-32-caracteres";

    private static ProtectorTotp Crear(string? clave)
    {
        var valores = new Dictionary<string, string?> { ["Jwt:Key"] = clave };
        var config = new ConfigurationBuilder().AddInMemoryCollection(valores).Build();
        return new ProtectorTotp(config);
    }

    [Fact]
    public void Proteger_y_Revelar_devuelven_el_texto_original()
    {
        var protector = Crear(ClaveA);

        var protegido = protector.Proteger("JBSWY3DPEHPK3PXP");

        Assert.Equal("JBSWY3DPEHPK3PXP", protector.Revelar(protegido));
    }

    [Fact]
    public void El_texto_protegido_no_contiene_el_secreto_en_claro()
    {
        var protegido = Crear(ClaveA).Proteger("JBSWY3DPEHPK3PXP");

        Assert.DoesNotContain("JBSWY3DPEHPK3PXP", protegido);
    }

    [Fact]
    public void Proteger_el_mismo_texto_dos_veces_da_resultados_distintos()
    {
        var protector = Crear(ClaveA);

        Assert.NotEqual(protector.Proteger("JBSWY3DPEHPK3PXP"), protector.Proteger("JBSWY3DPEHPK3PXP"));
    }

    [Fact]
    public void Revelar_con_otra_clave_devuelve_null()
    {
        var protegido = Crear(ClaveA).Proteger("JBSWY3DPEHPK3PXP");

        Assert.Null(Crear(ClaveB).Revelar(protegido));
    }

    [Fact]
    public void Revelar_un_valor_alterado_devuelve_null()
    {
        var protector = Crear(ClaveA);
        var bytes = Convert.FromBase64String(protector.Proteger("JBSWY3DPEHPK3PXP"));
        bytes[^1] ^= 0xFF;   // se altera el último byte del texto cifrado

        Assert.Null(protector.Revelar(Convert.ToBase64String(bytes)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("esto no es base64!!")]
    [InlineData("AAAA")]
    public void Revelar_valores_inexistentes_o_danados_devuelve_null(string? valor)
    {
        Assert.Null(Crear(ClaveA).Revelar(valor));
    }

    [Fact]
    public void Sin_Jwt_Key_no_se_puede_crear_el_protector()
    {
        Assert.Throws<InvalidOperationException>(() => Crear(null));
    }
}