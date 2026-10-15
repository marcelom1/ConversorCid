using System.Text;
using ConversorCid.Core.Tests.Apoio;
using ConversorCid.Planilhas;

namespace ConversorCid.Core.Tests;

public class CsvPlanilhaLeitorTests
{
    private static readonly CsvPlanilhaLeitor Leitor = new(';');

    private static Planilha Ler(byte[] bytes)
    {
        using var fluxo = new MemoryStream(bytes);
        return Leitor.Ler(fluxo);
    }

    private static Planilha Ler(string texto, Encoding? codificacao = null) => Ler((codificacao ?? new UTF8Encoding(false)).GetBytes(texto));

    [Fact]
    public void Windows1252_ComPontoEVirgulaNoFim_LeCabecalhoELinhas()
    {
        var planilha = Ler(DadosCid10.ArquivoDatasus("CAT;DESCRICAO", "A00;Cólera", "A01;Febres tifóide e paratifóide"));

        Assert.Equal(["CAT", "DESCRICAO", ""], planilha.Cabecalhos);
        Assert.Equal(2, planilha.Linhas.Count);
        Assert.Equal("Cólera", planilha.Linhas[0]["DESCRICAO"]);
        Assert.Equal("Febres tifóide e paratifóide", planilha.Linhas[1]["DESCRICAO"]);
        Assert.Equal([2, 3], planilha.Linhas.Select(l => l.Numero));
    }

    [Fact]
    public void Utf8ComBom_EmBomSemBom_SaoReconhecidos()
    {
        var comBom = Ler(new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes("A;B\r\nç;ã\r\n")).ToArray());
        var semBom = Ler("A;B\r\nç;ã\r\n");

        foreach (var planilha in new[] { comBom, semBom })
        {
            Assert.Equal(["A", "B"], planilha.Cabecalhos);   // o BOM não vira parte do nome da primeira coluna
            Assert.Equal(("ç", "ã"), (planilha.Linhas[0]["A"], planilha.Linhas[0]["B"]));
        }
    }

    [Fact]
    public void Aspas_SaoCaracteresDoTexto_NuncaDelimitadores()
    {
        var planilha = Ler(DadosCid10.ArquivoDatasus("COD;DESC",
            "F40;Transtornos neuróticos, relacionados com o \"stress\" e somatoformes",
            "M8040/1;\"Tumorlet\""));

        Assert.Equal("Transtornos neuróticos, relacionados com o \"stress\" e somatoformes", planilha.Linhas[0]["DESC"]);
        Assert.Equal("\"Tumorlet\"", planilha.Linhas[1]["DESC"]);
    }

    [Theory]
    [InlineData("\r\n")]
    [InlineData("\n")]
    [InlineData("\r")]
    public void QuebrasDeLinha_Crlf_Lf_Cr(string quebra)
    {
        var planilha = Ler($"A;B{quebra}1;2{quebra}3;4{quebra}");

        Assert.Equal(2, planilha.Linhas.Count);
        Assert.Equal("4", planilha.Linhas[1]["B"]);
    }

    [Fact]
    public void LinhasEmBranco_SaoPuladas_MasANumeracaoContinuaFiel()
    {
        var planilha = Ler("A;B\r\n1;2\r\n\r\n   \r\n3;4\r\n");

        Assert.Equal([2, 5], planilha.Linhas.Select(l => l.Numero));
    }

    [Fact]
    public void LinhaComMenosCampos_ECompletadaComVazio()
    {
        var planilha = Ler("A;B;C\r\n1;2\r\n");

        Assert.Equal(("1", "2", ""), (planilha.Linhas[0]["A"], planilha.Linhas[0]["B"], planilha.Linhas[0]["C"]));
    }

    [Fact]
    public void LinhaComMaisCamposQueOCabecalho_ERecusadaComONumeroDaLinha()
    {
        var ex = Assert.Throws<PlanilhaInvalidaException>(() => Ler("A;B\r\n1;2\r\n3;4;5\r\n"));

        Assert.Contains("linha 3", ex.Message);
        Assert.Contains("';'", ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("\r\n   \r\n")]
    public void ArquivoVazio_ERecusado(string texto)
    {
        Assert.Throws<PlanilhaInvalidaException>(() => Ler(texto));
    }

    [Fact]
    public void OutroDelimitador_Funciona()
    {
        using var fluxo = new MemoryStream("A|B\r\n1|2\r\n"u8.ToArray());

        var planilha = new CsvPlanilhaLeitor('|').Ler(fluxo);

        Assert.Equal("2", planilha.Linhas[0]["B"]);
    }

    [Fact]
    public void ColunaDeNomeVazio_NaoEEnderecavel()
    {
        var planilha = Ler("A;;B\r\n1;x;2\r\n");

        Assert.False(planilha.ContemColuna(""));
        Assert.Equal("2", planilha.Linhas[0]["B"]);
    }
}
