using ClosedXML.Excel;
using ConversorCid.Planilhas;
using static ConversorCid.Core.Tests.Apoio.Planilhas;

namespace ConversorCid.Core.Tests;

public class AbasXlsxTests : IDisposable
{
    private readonly string _pasta = Path.Combine(Path.GetTempPath(), "ConversorCidTests", Guid.NewGuid().ToString("N"));

    public AbasXlsxTests() => Directory.CreateDirectory(_pasta);

    public void Dispose()
    {
        if (Directory.Exists(_pasta))
            Directory.Delete(_pasta, recursive: true);
    }

    /// <summary>Um .xlsx real, com as abas na ordem dada e as ocultas marcadas.</summary>
    private static MemoryStream Livro(params (string Nome, XLWorksheetVisibility Visibilidade)[] abas)
    {
        using var livro = new XLWorkbook();
        foreach (var (nome, visibilidade) in abas)
        {
            var aba = livro.AddWorksheet(nome);
            aba.Cell(1, 1).Value = "Code";
            aba.Visibility = visibilidade;
        }

        var memoria = new MemoryStream();
        livro.SaveAs(memoria);
        memoria.Position = 0;
        return memoria;
    }

    [Fact]
    public void Lista_as_abas_na_ordem_do_Excel()
    {
        using var arquivo = Livro(("Plan1", XLWorksheetVisibility.Visible), ("Dados", XLWorksheetVisibility.Visible), ("Resumo", XLWorksheetVisibility.Visible));

        var abas = AbasXlsx.Listar(arquivo);

        Assert.Equal(["Plan1", "Dados", "Resumo"], abas.Select(a => a.Nome));
        Assert.All(abas, a => Assert.False(a.Oculta));
    }

    [Fact]
    public void Marca_as_abas_ocultas()
    {
        // O livro precisa de uma aba visível; a primeira aqui é a oculta, como na planilha da análise.
        using var arquivo = Livro(
            ("SimpleTabulation-ICD-11-MMS-pt", XLWorksheetVisibility.Hidden),
            ("BENNER", XLWorksheetVisibility.VeryHidden),
            ("DIVERGENTES", XLWorksheetVisibility.Visible));

        var abas = AbasXlsx.Listar(arquivo);

        Assert.Equal(
            [("SimpleTabulation-ICD-11-MMS-pt", true), ("BENNER", true), ("DIVERGENTES", false)],
            abas.Select(a => (a.Nome, a.Oculta)));
    }

    [Fact]
    public void A_primeira_aba_listada_e_a_que_a_conversao_le_sem_aba_indicada()
    {
        using var arquivo = Xlsx(("Primeira", ["Code"], []), ("Segunda", ["Code"], []));

        Assert.Equal("Primeira", AbasXlsx.Listar(arquivo)[0].Nome);
    }

    [Fact]
    public void Nao_fecha_o_fluxo_recebido()
    {
        using var arquivo = Livro(("Plan1", XLWorksheetVisibility.Visible));

        AbasXlsx.Listar(arquivo);

        Assert.True(arquivo.CanRead);
    }

    [Fact]
    public void Arquivo_que_nao_e_xlsx_e_recusado_com_mensagem_para_o_usuario()
    {
        using var texto = new MemoryStream("Code|Title"u8.ToArray());

        var ex = Assert.Throws<PlanilhaInvalidaException>(() => AbasXlsx.Listar(texto));

        Assert.Contains(".xlsx", ex.Message);
    }

    [Fact]
    public void Formato_antigo_xls_e_recusado_com_a_orientacao_de_salvar_como_xlsx()
    {
        using var xls = new MemoryStream([0xD0, 0xCF, 0x11, 0xE0, 0x00, 0x00]);

        var ex = Assert.Throws<PlanilhaInvalidaException>(() => AbasXlsx.Listar(xls));

        Assert.Contains(".xls", ex.Message);
    }

    [Fact]
    public void Zip_que_nao_e_planilha_e_recusado()
    {
        using var zip = new MemoryStream();
        using (var arquivo = new System.IO.Compression.ZipArchive(zip, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
            arquivo.CreateEntry("qualquer.txt");
        zip.Position = 0;

        Assert.Throws<PlanilhaInvalidaException>(() => AbasXlsx.Listar(zip));
    }

    [Fact]
    public void Le_do_arquivo_mesmo_com_ele_aberto_em_outro_programa()
    {
        var caminho = Path.Combine(_pasta, "livro.xlsx");
        File.WriteAllBytes(caminho, Livro(("Plan1", XLWorksheetVisibility.Visible)).ToArray());

        using var aberto = new FileStream(caminho, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);

        Assert.Equal(["Plan1"], AbasXlsx.Listar(caminho).Select(a => a.Nome));
    }

    [Fact]
    public void Arquivo_que_nao_existe_falha_com_FileNotFound()
    {
        Assert.Throws<FileNotFoundException>(() => AbasXlsx.Listar(Path.Combine(_pasta, "nao-existe.xlsx")));
    }
}
