using System.Text;
using ConversorCid.Conversao;
using ConversorCid.Perfis;
using ConversorCid.Planilhas;
using static ConversorCid.Core.Tests.Apoio.Planilhas;

namespace ConversorCid.Core.Tests;

public class ConversorParaCsvTests : IDisposable
{
    private readonly string _pasta = Path.Combine(Path.GetTempPath(), "ConversorCidTests", Guid.NewGuid().ToString("N"));
    private readonly PerfilCid11 _perfil = new();
    private readonly ConversorParaCsv _conversor = new();

    public ConversorParaCsvTests() => Directory.CreateDirectory(_pasta);

    public void Dispose()
    {
        if (Directory.Exists(_pasta))
            Directory.Delete(_pasta, recursive: true);
    }

    private string Caminho(string nome) => Path.Combine(_pasta, nome);

    private string SalvarXlsx(string nome, string[] cabecalhos, params object?[][] linhas)
    {
        var caminho = Caminho(nome);
        File.WriteAllBytes(caminho, Xlsx(cabecalhos, linhas).ToArray());
        return caminho;
    }

    private static string Texto(MemoryStream csv) => new UTF8Encoding(false).GetString(csv.ToArray(), 3, (int)csv.Length - 3);

    /// <summary>Uma linha do CSV a partir dos 13 campos, para o teste não depender de contar barras.</summary>
    private static string Linha(params string[] campos)
    {
        Assert.Equal(13, campos.Length);
        return string.Join('|', campos);
    }

    private static string Csv(params string[] linhas) => string.Concat(linhas.Select(l => l + "\r\n"));

    [Fact]
    public void FimAFim_XlsxNoLayoutDaOms_GeraOCsvDeImportacao()
    {
        var xlsx = Xlsx(
            CabecalhoOms,
            LinhaOms(title: "Algumas doenças infecciosas ou parasitárias", classKind: "chapter", chapterNo: "01", isLeaf: "False"),
            LinhaOms(blockId: "BlockL1-1A0", title: "- Gastroenterites ou colites de origem infecciosa", classKind: "block", isLeaf: "False"),
            LinhaOms(blockId: "BlockL2-1A0", title: "- - Infecções intestinais bacterianas", classKind: "block", isLeaf: "False", g1: "BlockL1-1A0"),
            LinhaOms(code: "1A00", title: "- - - Cólera", g1: "BlockL1-1A0", g2: "BlockL2-1A0"),
            LinhaOms(code: "1A00.0", title: "- - - - Cólera clássica", g1: "BlockL1-1A0", g2: "BlockL2-1A0"),
            LinhaOms(code: "1H0Z", title: "- - Doença infecciosa ou parasitária, não especificada", g1: "BlockL1-1H0"));
        using var csv = new MemoryStream();

        var resultado = _conversor.Converter(xlsx, csv, _perfil);

        Assert.Equal(
            Csv(
                Linha("Code", "BlockId", "Title", "ClassKind", "ChapterNo", "isLeaf", "Grouping1", "Grouping2", "Grouping3", "Grouping4", "Grouping5", "Intervalos Inicio", "Intervalos Fim"),
                Linha("", "", "Algumas doenças infecciosas ou parasitárias", "chapter", "1", "FALSE", "", "", "", "", "", "1A00", "1H0Z"),
                Linha("", "BlockL1-1A0", "Gastroenterites ou colites de origem infecciosa", "block", "1", "FALSE", "", "", "", "", "", "", ""),
                Linha("", "BlockL2-1A0", "Infecções intestinais bacterianas", "block", "1", "FALSE", "BlockL1-1A0", "", "", "", "", "", ""),
                Linha("1A00", "", "Cólera", "category", "1", "TRUE", "BlockL1-1A0", "BlockL2-1A0", "", "", "", "", ""),
                Linha("1A00.0", "", "Cólera clássica", "category", "1", "TRUE", "BlockL1-1A0", "BlockL2-1A0", "", "", "", "", ""),
                Linha("1H0Z", "", "Doença infecciosa ou parasitária, não especificada", "category", "1", "TRUE", "BlockL1-1H0", "", "", "", "", "", "")),
            Texto(csv));
        Assert.Equal(new[] { 0xEF, 0xBB, 0xBF }, csv.ToArray().Take(3).Select(b => (int)b));
        Assert.Equal(("cid11", "Plan1", 6, 6), (resultado.Perfil, resultado.Entradas[0].Origem, resultado.LinhasLidas, resultado.LinhasGravadas));
        Assert.Empty(resultado.Avisos);
    }

    [Fact]
    public void ChapterNo_TextoComZeroOuNumero_VaiParaOMesmoValor()
    {
        var xlsx = Xlsx(
            CabecalhoOms,
            LinhaOms(code: "1A00", title: "a", chapterNo: "01"),
            LinhaOms(code: "2A00", title: "b", chapterNo: 2),
            LinhaOms(code: "2B00", title: "c", chapterNo: 2.0));
        using var csv = new MemoryStream();

        _conversor.Converter(xlsx, csv, _perfil);

        var capitulos = Texto(csv).Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Skip(1).Select(l => l.Split('|')[4]);
        Assert.Equal(["1", "2", "2"], capitulos);
    }

    [Fact]
    public void IsLeaf_ComoBooleanoDoExcel_VaiParaTrueFalse()
    {
        var xlsx = Xlsx(
            CabecalhoOms,
            LinhaOms(code: "1A00", title: "a").Select((v, i) => i == 11 ? true : v).ToArray(),
            LinhaOms(code: "1A01", title: "b").Select((v, i) => i == 11 ? false : v).ToArray());
        using var csv = new MemoryStream();

        _conversor.Converter(xlsx, csv, _perfil);

        var folhas = Texto(csv).Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Skip(1).Select(l => l.Split('|')[5]);
        Assert.Equal(["TRUE", "FALSE"], folhas);
    }

    [Fact]
    public void LinhasEmBranco_SaoIgnoradas_EOsAvisosUsamONumeroDaLinhaDoExcel()
    {
        var linhaVazia = new object?[CabecalhoOms.Length];
        var xlsx = Xlsx(
            CabecalhoOms,
            LinhaOms(code: "1A00", title: "ok"),     // linha 2
            linhaVazia,                               // linha 3
            LinhaOms(code: "1A01", title: "a|b"));    // linha 4
        using var csv = new MemoryStream();

        var resultado = _conversor.Converter(xlsx, csv, _perfil);

        Assert.Equal(2, resultado.LinhasLidas);
        Assert.Equal(4, Assert.Single(resultado.Avisos).Linha);
    }

    [Fact]
    public void ColunasFaltando_ListaAsColunasEOPerfil()
    {
        var xlsx = Xlsx(["Code", "Title"], ["1A00", "x"]);
        using var csv = new MemoryStream();

        var ex = Assert.Throws<PlanilhaInvalidaException>(() => _conversor.Converter(xlsx, csv, _perfil));

        Assert.Contains("cid11", ex.Message);
        Assert.Contains("ClassKind", ex.Message);
        Assert.Contains("Grouping5", ex.Message);
        Assert.DoesNotContain("Code,", ex.Message);   // a que existe não é listada
    }

    [Fact]
    public void ArquivoQueNaoEXlsx_DizQueNaoEPlanilha()
    {
        using var texto = new MemoryStream("isto é só um texto"u8.ToArray());
        using var csv = new MemoryStream();

        var ex = Assert.Throws<PlanilhaInvalidaException>(() => _conversor.Converter(texto, csv, _perfil));

        Assert.Contains(".xlsx", ex.Message);
    }

    [Fact]
    public void XlsAntigo_PedeParaSalvarComoXlsx()
    {
        using var xls = new MemoryStream([0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1]);
        using var csv = new MemoryStream();

        var ex = Assert.Throws<PlanilhaInvalidaException>(() => _conversor.Converter(xls, csv, _perfil));

        Assert.Contains(".xls", ex.Message);
        Assert.Contains("salve como .xlsx", ex.Message);
    }

    [Fact]
    public void ZipQueNaoEPlanilha_DizQueEstaCorrompido()
    {
        using var zipVazio = new MemoryStream([0x50, 0x4B, 0x05, 0x06, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]);
        using var csv = new MemoryStream();

        Assert.Throws<PlanilhaInvalidaException>(() => _conversor.Converter(zipVazio, csv, _perfil));
    }

    [Fact]
    public void AbaPeloNome_ESemInformarAPrimeira()
    {
        var xlsx = Xlsx(
            ("Resumo", ["X"], [["nada"]]),
            ("Dados", CabecalhoOms, [LinhaOms(code: "1A00", title: "a")]));
        using var csv = new MemoryStream();

        var resultado = _conversor.Converter(xlsx, csv, _perfil, aba: "dados");   // caixa não importa

        Assert.Equal("Dados", resultado.Entradas[0].Origem);
        Assert.Equal(1, resultado.LinhasGravadas);
    }

    [Fact]
    public void AbaInexistente_ListaAsAbasDisponiveis()
    {
        var xlsx = Xlsx(("Resumo", ["X"], [["nada"]]), ("Dados", CabecalhoOms, []));
        using var csv = new MemoryStream();

        var ex = Assert.Throws<PlanilhaInvalidaException>(() => _conversor.Converter(xlsx, csv, _perfil, aba: "Outra"));

        Assert.Contains("'Outra'", ex.Message);
        Assert.Contains("'Resumo'", ex.Message);
        Assert.Contains("'Dados'", ex.Message);
    }

    [Fact]
    public void Arquivo_GravaOCsvEDevolveOCaminho()
    {
        var entrada = SalvarXlsx("cid.xlsx", CabecalhoOms, LinhaOms(code: "1A00", title: "- - - Cólera"));
        var saida = Caminho("cid.csv");

        var resultado = _conversor.Converter(entrada, saida, _perfil);

        Assert.Equal(saida, resultado.CaminhoSaida);
        Assert.Equal(["EF", "BB", "BF"], File.ReadAllBytes(saida).Take(3).Select(b => b.ToString("X2")));
        Assert.Contains("1A00||Cólera|category|1|TRUE", File.ReadAllText(saida));
        Assert.Empty(Directory.GetFiles(_pasta, "*.tmp"));
    }

    [Fact]
    public void Arquivo_SaidaJaExiste_SemSobrescrever_RecusaENaoAltera()
    {
        var entrada = SalvarXlsx("cid.xlsx", CabecalhoOms, LinhaOms(code: "1A00", title: "a"));
        var saida = Caminho("cid.csv");
        File.WriteAllText(saida, "conteúdo anterior");

        var ex = Assert.Throws<IOException>(() => _conversor.Converter(entrada, saida, _perfil));

        Assert.Contains("já existe", ex.Message);
        Assert.Equal("conteúdo anterior", File.ReadAllText(saida));
    }

    [Fact]
    public void Arquivo_SaidaJaExiste_ComSobrescrever_Substitui()
    {
        var entrada = SalvarXlsx("cid.xlsx", CabecalhoOms, LinhaOms(code: "1A00", title: "a"));
        var saida = Caminho("cid.csv");
        File.WriteAllText(saida, "conteúdo anterior");

        _conversor.Converter(entrada, saida, _perfil, sobrescrever: true);

        var bytes = File.ReadAllBytes(saida);
        Assert.Equal(["EF", "BB", "BF"], bytes.Take(3).Select(b => b.ToString("X2")));
        Assert.StartsWith("Code|BlockId", File.ReadAllText(saida));   // ReadAllText já descarta o BOM

    }

    [Fact]
    public void Arquivo_FalhaNaConversao_PreservaASaidaExistenteENaoDeixaTemporario()
    {
        var entrada = SalvarXlsx("ruim.xlsx", ["Code"], ["1A00"]);   // faltam colunas
        var saida = Caminho("cid.csv");
        File.WriteAllText(saida, "conteúdo anterior");

        Assert.Throws<PlanilhaInvalidaException>(() => _conversor.Converter(entrada, saida, _perfil, sobrescrever: true));

        Assert.Equal("conteúdo anterior", File.ReadAllText(saida));
        Assert.Empty(Directory.GetFiles(_pasta, "*.tmp"));
    }

    [Fact]
    public void Arquivo_EntradaIgualASaida_Recusa()
    {
        var entrada = SalvarXlsx("cid.xlsx", CabecalhoOms, LinhaOms(code: "1A00", title: "a"));

        Assert.Throws<IOException>(() => _conversor.Converter(entrada, entrada.ToUpperInvariant(), _perfil, sobrescrever: true));
    }

    [Fact]
    public void Arquivo_EntradaInexistente_LancaFileNotFound()
    {
        Assert.Throws<FileNotFoundException>(() => _conversor.Converter(Caminho("nao-existe.xlsx"), Caminho("x.csv"), _perfil));
    }

    [Fact]
    public void Arquivo_PlanilhaAbertaEmOutroProcesso_ComoOExcelFaz_AindaConverte()
    {
        var entrada = SalvarXlsx("cid.xlsx", CabecalhoOms, LinhaOms(code: "1A00", title: "a"));
        using var aberta = new FileStream(entrada, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);

        var resultado = _conversor.Converter(entrada, Caminho("cid.csv"), _perfil);

        Assert.Equal(1, resultado.LinhasGravadas);
    }

    [Fact]
    public void Arquivo_CriaAPastaDeSaidaSeNaoExistir()
    {
        var entrada = SalvarXlsx("cid.xlsx", CabecalhoOms, LinhaOms(code: "1A00", title: "a"));
        var saida = Path.Combine(_pasta, "nova", "subpasta", "cid.csv");

        _conversor.Converter(entrada, saida, _perfil);

        Assert.True(File.Exists(saida));
    }

    [Fact]
    public void Catalogo_BuscaPerfilSemDiferenciarCaixa_EDevolveNuloParaDesconhecido()
    {
        Assert.IsType<PerfilCid11>(CatalogoPerfis.Buscar(" CID11 "));
        Assert.Null(CatalogoPerfis.Buscar("cid99"));
    }

    // ---------- perfis de várias entradas (CID-10: pasta com os CSVs do DATASUS) ----------

    private readonly PerfilCid10 _perfil10 = new();

    private string PastaDatasus(bool minusculas = false)
    {
        var pasta = Caminho("datasus");
        Apoio.DadosCid10.GravarPasta(pasta, minusculas);
        return pasta;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]   // o nome do arquivo é comparado sem diferenciar caixa
    public void Pasta_Cid10_GeraOCsvAPartirDosQuatroArquivos(bool nomesEmMinusculas)
    {
        var pasta = PastaDatasus(nomesEmMinusculas);
        var saida = Caminho("Cid10.csv");

        var resultado = _conversor.Converter(pasta, saida, _perfil10);

        Assert.Equal(["capitulos", "grupos", "categorias", "subcategorias"], resultado.Entradas.Select(e => e.Nome));
        Assert.Equal(["CID-10-CAPITULOS.CSV", "CID-10-GRUPOS.CSV", "CID-10-CATEGORIAS.CSV", "CID-10-SUBCATEGORIAS.CSV"], resultado.Entradas.Select(e => e.Origem));
        Assert.Equal((2 + 2 + 3 + 3, 2 + 2 + 3 + 2), (resultado.LinhasLidas, resultado.LinhasGravadas));   // A09 (folha) aparece nos dois arquivos e sai uma vez
        Assert.Empty(resultado.Avisos);

        var linhas = File.ReadAllText(saida).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("Code|BlockId|Title|ClassKind|ChapterNo|isLeaf|Grouping1|Grouping2|Grouping3|Grouping4|Grouping5|Intervalos Inicio|Intervalos Fim|Classification|Reference", linhas[0]);
        Assert.Equal("||Algumas doenças infecciosas e parasitárias|chapter|1|FALSE||||||A00|B99||", linhas[1]);
        Assert.Contains(string.Join('|', ["A000", "", "Cólera devida a Vibrio cholerae 01, biótipo cholerae", "subcategory", "1", "TRUE", "A00-A09", "", "", "", "", "", "", "", ""]), linhas);
        Assert.Equal(["EF", "BB", "BF"], File.ReadAllBytes(saida).Take(3).Select(b => b.ToString("X2")));
        Assert.Empty(Directory.GetFiles(_pasta, "*.tmp"));
    }

    [Fact]
    public void Pasta_FaltandoArquivos_ListaOsQueFaltam()
    {
        var pasta = PastaDatasus();
        File.Delete(Path.Combine(pasta, "CID-10-GRUPOS.CSV"));
        File.Delete(Path.Combine(pasta, "CID-10-CATEGORIAS.CSV"));

        var ex = Assert.Throws<FileNotFoundException>(() => _conversor.Converter(pasta, Caminho("x.csv"), _perfil10));

        Assert.Contains("CID-10-GRUPOS.CSV", ex.Message);
        Assert.Contains("CID-10-CATEGORIAS.CSV", ex.Message);
        Assert.DoesNotContain("CID-10-CAPITULOS.CSV", ex.Message);
        Assert.False(File.Exists(Caminho("x.csv")));
    }

    [Fact]
    public void Pasta_ArquivoSemAsColunasExigidas_DizQualArquivoEQuaisColunas()
    {
        var pasta = PastaDatasus();
        File.WriteAllBytes(Path.Combine(pasta, "CID-10-GRUPOS.CSV"), Apoio.DadosCid10.ArquivoDatasus("INICIO;FIM", "A00;A09"));

        var ex = Assert.Throws<PlanilhaInvalidaException>(() => _conversor.Converter(pasta, Caminho("x.csv"), _perfil10));

        Assert.Contains("CID-10-GRUPOS.CSV", ex.Message);
        Assert.Contains("CATINIC", ex.Message);
        Assert.Contains("DESCRICAO", ex.Message);
    }

    [Fact]
    public void Pasta_ArquivoIlegivel_ComVariosArquivos_DizQualFalhou()
    {
        var pasta = PastaDatasus();
        File.WriteAllBytes(Path.Combine(pasta, "CID-10-CATEGORIAS.CSV"), Apoio.DadosCid10.ArquivoDatasus("CAT;CLASSIF;DESCRICAO;REFER", "A00;;Texto;;com;ponto-e-virgula;demais"));

        var ex = Assert.Throws<PlanilhaInvalidaException>(() => _conversor.Converter(pasta, Caminho("x.csv"), _perfil10));

        Assert.StartsWith("CID-10-CATEGORIAS.CSV:", ex.Message);
        Assert.Contains("linha 2", ex.Message);
    }

    [Fact]
    public void PerfilDeVariosArquivos_RecebendoUmArquivo_PedeAPasta()
    {
        var pasta = PastaDatasus();

        var ex = Assert.Throws<PlanilhaInvalidaException>(() => _conversor.Converter(Path.Combine(pasta, "CID-10-GRUPOS.CSV"), Caminho("x.csv"), _perfil10));

        Assert.Contains("pasta", ex.Message);
    }

    [Fact]
    public void PerfilDeArquivoUnico_RecebendoUmaPasta_PedeOArquivo()
    {
        var ex = Assert.Throws<PlanilhaInvalidaException>(() => _conversor.Converter(PastaDatasus(), Caminho("x.csv"), _perfil));

        Assert.Contains("informe o arquivo", ex.Message);
    }

    [Fact]
    public void EntradaInexistente_Pasta_LancaFileNotFound()
    {
        Assert.Throws<FileNotFoundException>(() => _conversor.Converter(Caminho("nao-existe"), Caminho("x.csv"), _perfil10));
    }

    [Fact]
    public void SaidaIgualAUmDosArquivosDeEntrada_Recusa()
    {
        var pasta = PastaDatasus();

        Assert.Throws<IOException>(() => _conversor.Converter(pasta, Path.Combine(pasta, "cid-10-grupos.csv"), _perfil10, sobrescrever: true));
        Assert.Contains("A00", File.ReadAllText(Path.Combine(pasta, "CID-10-GRUPOS.CSV")));   // a entrada continua intacta
    }

    [Fact]
    public void FluxoUnico_ComPerfilDeVariosArquivos_ERecusado()
    {
        using var qualquer = new MemoryStream();
        using var csv = new MemoryStream();

        Assert.Throws<ArgumentException>(() => _conversor.Converter(qualquer, csv, _perfil10));
    }

    [Fact]
    public void Fluxos_Cid10_ConverteSemTocarODisco()
    {
        var entradas = new Dictionary<string, Stream>
        {
            ["capitulos"] = new MemoryStream(Apoio.DadosCid10.ArquivoDatasus("NUMCAP;CATINIC;CATFIM;DESCRICAO", "1;A00;B99;Capítulo I - Infecciosas")),
            ["grupos"] = new MemoryStream(Apoio.DadosCid10.ArquivoDatasus("CATINIC;CATFIM;DESCRICAO", "A00;A09;Intestinais")),
            ["categorias"] = new MemoryStream(Apoio.DadosCid10.ArquivoDatasus("CAT;CLASSIF;DESCRICAO;REFER", "A00;;Cólera;")),
            ["subcategorias"] = new MemoryStream(Apoio.DadosCid10.ArquivoDatasus("SUBCAT;CLASSIF;DESCRICAO;REFER", "A000;;Cólera tipo 0;")),
        };
        using var csv = new MemoryStream();

        var resultado = _conversor.Converter(entradas, csv, _perfil10);

        Assert.Equal(4, resultado.LinhasGravadas);
        Assert.Contains("A000||Cólera tipo 0|subcategory|1|TRUE|A00-A09", Texto(csv));
    }

    [Fact]
    public void Catalogo_TemOsDoisPerfis()
    {
        Assert.Equal(["cid10", "cid11"], CatalogoPerfis.Todos.Select(p => p.Nome).Order());
        Assert.IsType<PerfilCid10>(CatalogoPerfis.Buscar("CID10"));
    }
}
