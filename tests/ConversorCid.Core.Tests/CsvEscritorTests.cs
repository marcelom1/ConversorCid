using System.Text;
using ConversorCid.Saida;

namespace ConversorCid.Core.Tests;

public class CsvEscritorTests
{
    private static byte[] Gravar(IReadOnlyList<string> cabecalho, params string[][] linhas)
    {
        using var memoria = new MemoryStream();
        CsvEscritor.Escrever(memoria, cabecalho, linhas);
        return memoria.ToArray();
    }

    [Fact]
    public void Formato_Bom_Crlf_DelimitadorBarra_SemAspas_CrlfNoFim()
    {
        var bytes = Gravar(["Code", "Title"], ["1A00", "Cólera"], ["", "Sem código"]);

        Assert.Equal([0xEF, 0xBB, 0xBF], bytes[..3]);
        Assert.Equal("Code|Title\r\n1A00|Cólera\r\n|Sem código\r\n", Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3));
    }

    [Fact]
    public void Acentos_SaemEmUtf8()
    {
        var bytes = Gravar(["T"], ["çã"]);

        // ç = C3 A7, ã = C3 A3
        Assert.Contains((byte)0xC3, bytes);
        Assert.Equal("T\r\nçã\r\n", Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3));
    }

    [Fact]
    public void SoCabecalho_GravaCabecalhoEBom()
    {
        var bytes = Gravar(["A", "B"]);

        Assert.Equal("A|B\r\n", Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3));
    }

    [Theory]
    [InlineData("a|b")]
    [InlineData("a\nb")]
    [InlineData("a\rb")]
    public void Recusa_CampoComDelimitadorOuQuebraDeLinha(string campo)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Gravar(["T"], [campo]));
        Assert.Contains("sem aspas", ex.Message);
    }

    [Fact]
    public void Recusa_LinhaComLarguraDiferenteDoCabecalho()
    {
        Assert.Throws<InvalidOperationException>(() => Gravar(["A", "B"], ["so um"]));
    }
}
