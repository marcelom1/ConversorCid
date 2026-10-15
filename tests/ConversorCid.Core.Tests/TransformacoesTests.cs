using ConversorCid.Perfis;

namespace ConversorCid.Core.Tests;

public class TransformacoesTests
{
    [Theory]
    [InlineData("- - - Cólera", "Cólera")]
    [InlineData("- Gastroenterites ou colites", "Gastroenterites ou colites")]
    [InlineData("- - - - - - Cisticercose", "Cisticercose")]
    [InlineData("Neoplasias", "Neoplasias")]
    [InlineData("  - - Cólera  ", "Cólera")]
    [InlineData("- - X-ray", "X-ray")]                         // hífen que é do texto fica
    [InlineData("Leucemia - tipo A", "Leucemia - tipo A")]     // só o prefixo é marcador
    [InlineData("-", "")]
    [InlineData("- - ", "")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void RemoverMarcadoresDeNivel(string? entrada, string esperado) =>
        Assert.Equal(esperado, Transformacoes.RemoverMarcadoresDeNivel(entrada));

    [Theory]
    [InlineData("01", "1")]
    [InlineData("02", "2")]
    [InlineData("10", "10")]
    [InlineData("26", "26")]
    [InlineData("00", "0")]
    [InlineData("V", "V")]
    [InlineData("X", "X")]
    [InlineData(" 07 ", "7")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void NormalizarNumeroDeCapitulo(string? entrada, string esperado) =>
        Assert.Equal(esperado, Transformacoes.NormalizarNumeroDeCapitulo(entrada));

    [Theory]
    [InlineData("True", "TRUE")]
    [InlineData("false", "FALSE")]
    [InlineData("TRUE", "TRUE")]
    [InlineData(" Falso ", "FALSE")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void TentarNormalizarBooleano_reconhece(string? entrada, string esperado)
    {
        Assert.True(Transformacoes.TentarNormalizarBooleano(entrada, out var resultado));
        Assert.Equal(esperado, resultado);
    }

    [Theory]
    [InlineData("talvez")]
    [InlineData("2")]
    public void TentarNormalizarBooleano_naoReconhece_devolveOOriginal(string entrada)
    {
        Assert.False(Transformacoes.TentarNormalizarBooleano(entrada, out var resultado));
        Assert.Equal(entrada, resultado);
    }

    [Theory]
    [InlineData("nocardiose|A43.8 Outras", "nocardiose A43.8 Outras", true)]
    [InlineData("a | b", "a b", true)]                      // não sobram espaços duplos
    [InlineData("grandes|células", "grandes células", true)]
    [InlineData("linha1\r\nlinha2", "linha1 linha2", false)]
    [InlineData("com\ttab", "com tab", false)]
    [InlineData("  espaços   demais  ", "espaços demais", false)]
    [InlineData("Cólera", "Cólera", false)]
    [InlineData("", "", false)]
    [InlineData(null, "", false)]
    public void LimparCampo(string? entrada, string esperado, bool substituiuDelimitador)
    {
        Assert.Equal(esperado, Transformacoes.LimparCampo(entrada, '|', out var substituiu));
        Assert.Equal(substituiuDelimitador, substituiu);
    }

    [Theory]
    [InlineData("Capítulo I - Algumas doenças infecciosas", "Algumas doenças infecciosas")]
    [InlineData("Capítulo III  - Doenças do sangue", "Doenças do sangue")]
    [InlineData("Capítulo XVIII - Sintomas", "Sintomas")]
    [InlineData("CAPITULO II - Neoplasias", "Neoplasias")]
    [InlineData("Capítulo II - Neoplasias - tumores", "Neoplasias - tumores")]   // só o primeiro hífen
    [InlineData("Neoplasias [tumores]", "Neoplasias [tumores]")]
    [InlineData("Capitulares", "Capitulares")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void RemoverPrefixoDeCapitulo(string? entrada, string esperado) =>
        Assert.Equal(esperado, Transformacoes.RemoverPrefixoDeCapitulo(entrada));
}
