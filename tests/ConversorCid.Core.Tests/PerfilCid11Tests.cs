using ConversorCid.Conversao;
using ConversorCid.Core.Tests.Apoio;
using ConversorCid.Perfis;
using static ConversorCid.Core.Tests.Apoio.Planilhas;

namespace ConversorCid.Core.Tests;

public class PerfilCid11Tests
{
    private const int Codigo = 0, Bloco = 1, Titulo = 2, Tipo = 3, Capitulo = 4, Folha = 5, Grupo1 = 6, Grupo2 = 7, IntervaloInicio = 11, IntervaloFim = 12;

    private static (IReadOnlyList<string[]> Linhas, ColetorAvisos Avisos) Converter(params object?[][] linhas)
    {
        var avisos = new ColetorAvisos();
        return (new PerfilCid11().Converter(EmMemoriaOms(linhas), avisos), avisos);
    }

    [Fact]
    public void ColunasDeSaida_SaoAsDoCsvDeReferencia()
    {
        Assert.Equal(
            "Code|BlockId|Title|ClassKind|ChapterNo|isLeaf|Grouping1|Grouping2|Grouping3|Grouping4|Grouping5|Intervalos Inicio|Intervalos Fim",
            string.Join('|', new PerfilCid11().ColunasSaida));
    }

    [Fact]
    public void ColunasObrigatorias_SaoSoAsQueEntramNoCsv()
    {
        var obrigatorias = Assert.Single(new PerfilCid11().Entradas).ColunasObrigatorias;

        Assert.DoesNotContain("TitleEN", obrigatorias);
        Assert.DoesNotContain("Foundation URI", obrigatorias);
        Assert.Contains("Code", obrigatorias);
        Assert.Contains("Grouping5", obrigatorias);
        Assert.DoesNotContain("Intervalos Inicio", obrigatorias); // é calculada, não vem da planilha
    }

    [Fact]
    public void Categoria_MapeiaAsColunasETrataOsValores()
    {
        var (linhas, avisos) = Converter(
            LinhaOms(code: "1A00", title: "- - - Cólera", classKind: "category", chapterNo: "01", isLeaf: "True", g1: "BlockL1-1A0", g2: "BlockL2-1A0"));

        var saida = Assert.Single(linhas);
        Assert.Equal(["1A00", "", "Cólera", "category", "1", "TRUE", "BlockL1-1A0", "BlockL2-1A0", "", "", "", "", ""], saida);
        Assert.Empty(avisos.Avisos);
    }

    [Fact]
    public void Bloco_NaoPreencheIntervalos()
    {
        var (linhas, _) = Converter(
            LinhaOms(blockId: "BlockL1-1A0", title: "- Gastroenterites", classKind: "block", isLeaf: "False"),
            LinhaOms(code: "1A00", title: "- - - Cólera"));

        Assert.Equal("", linhas[0][IntervaloInicio]);
        Assert.Equal("", linhas[0][IntervaloFim]);
        Assert.Equal("", linhas[1][IntervaloInicio]);
        Assert.Equal("", linhas[1][IntervaloFim]);
    }

    [Fact]
    public void Capitulo_IntervaloEMenorEMaiorCodigoDasCategoriasDoCapitulo()
    {
        // fora de ordem de propósito, com subcódigos (que não contam) e categorias de outro capítulo
        var (linhas, avisos) = Converter(
            LinhaOms(title: "Doenças infecciosas", classKind: "chapter", chapterNo: "01", isLeaf: "False"),
            LinhaOms(code: "1B10", title: "- - A", chapterNo: "01"),
            LinhaOms(code: "1H0Z", title: "- - B", chapterNo: "01"),
            LinhaOms(code: "1A00", title: "- - C", chapterNo: "01"),
            LinhaOms(code: "1A00.0", title: "- - - D", chapterNo: "01"),
            LinhaOms(code: "1A00.0Z", title: "- - - E", chapterNo: "01"),
            LinhaOms(title: "Neoplasias", classKind: "chapter", chapterNo: "02", isLeaf: "False"),
            LinhaOms(code: "2A00", title: "- - F", chapterNo: "02"),
            LinhaOms(code: "2B30", title: "- - G", chapterNo: "02"));

        Assert.Equal(("1A00", "1H0Z"), (linhas[0][IntervaloInicio], linhas[0][IntervaloFim]));
        Assert.Equal(("2A00", "2B30"), (linhas[6][IntervaloInicio], linhas[6][IntervaloFim]));
        Assert.Empty(avisos.Avisos);
    }

    [Fact]
    public void Capitulo_CasaComAsCategoriasMesmoComZeroAEsquerdaDiferente()
    {
        // capítulo gravado como "01" num lado e como número 1 no outro
        var (linhas, _) = Converter(
            LinhaOms(title: "Cap", classKind: "chapter", chapterNo: 1, isLeaf: "False"),
            LinhaOms(code: "1A00", title: "- - C", chapterNo: "01"));

        Assert.Equal(("1A00", "1A00"), (linhas[0][IntervaloInicio], linhas[0][IntervaloFim]));
    }

    [Fact]
    public void Capitulo_SemCategorias_FicaSemIntervaloEAvisa()
    {
        var (linhas, avisos) = Converter(
            LinhaOms(title: "Vazio", classKind: "chapter", chapterNo: "05", isLeaf: "False"));

        Assert.Equal("", linhas[0][IntervaloInicio]);
        Assert.Equal("", linhas[0][IntervaloFim]);
        var aviso = Assert.Single(avisos.Avisos);
        Assert.Equal(2, aviso.Linha);
        Assert.Contains("capítulo '5'", aviso.Mensagem);
    }

    [Fact]
    public void Titulo_ComBarraVertical_TrocaPorEspacoEAvisaComALinhaDaPlanilha()
    {
        var (linhas, avisos) = Converter(
            LinhaOms(code: "1C1A", title: "- - - Ok"),
            LinhaOms(code: "1C1B.Y", title: "- - - Outras formas especificadas de nocardiose|A43.8 Outras formas de nocardiose"));

        Assert.Equal("Outras formas especificadas de nocardiose A43.8 Outras formas de nocardiose", linhas[1][Titulo]);
        var aviso = Assert.Single(avisos.Avisos);
        Assert.Equal(3, aviso.Linha);
        Assert.Contains("1C1B.Y", aviso.Mensagem);
        Assert.Contains("Title", aviso.Mensagem);
    }

    [Fact]
    public void BarraVerticalEmQualquerColuna_NuncaChegaAoCsv()
    {
        var (linhas, avisos) = Converter(LinhaOms(code: "1A|00", blockId: "B|L", title: "t", g1: "x|y"));

        Assert.All(linhas[0], campo => Assert.DoesNotContain('|', campo));
        Assert.Equal(3, avisos.Avisos.Count);
    }

    [Fact]
    public void TituloVazio_GravaEmBrancoEAvisa()
    {
        var (linhas, avisos) = Converter(LinhaOms(code: "1A36.0Y", title: ""));

        Assert.Equal("", linhas[0][Titulo]);
        var aviso = Assert.Single(avisos.Avisos);
        Assert.Equal(2, aviso.Linha);
        Assert.Contains("1A36.0Y", aviso.Mensagem);
        Assert.Contains("sem título", aviso.Mensagem);
    }

    [Fact]
    public void ClassKindDesconhecido_AvisaMasConverte()
    {
        var (linhas, avisos) = Converter(LinhaOms(code: "1A00", title: "t", classKind: "section"));

        Assert.Equal("section", linhas[0][Tipo]);
        Assert.Contains("ClassKind", Assert.Single(avisos.Avisos).Mensagem);
    }

    [Fact]
    public void IsLeafInvalido_MantemOValorEAvisa()
    {
        var (linhas, avisos) = Converter(LinhaOms(code: "1A00", title: "t", isLeaf: "talvez"));

        Assert.Equal("talvez", linhas[0][Folha]);
        Assert.Contains("isLeaf", Assert.Single(avisos.Avisos).Mensagem);
    }

    [Fact]
    public void ChapterNoNaoNumerico_Permanece()
    {
        var (linhas, _) = Converter(LinhaOms(code: "XA00", title: "t", chapterNo: "X"));

        Assert.Equal("X", linhas[0][Capitulo]);
    }

    [Fact]
    public void MantemAOrdemEAQuantidadeDeLinhas()
    {
        var (linhas, _) = Converter(
            LinhaOms(code: "1B00", title: "b"),
            LinhaOms(code: "1A00", title: "a"),
            LinhaOms(blockId: "BlockL1-1A0", title: "blk", classKind: "block"));

        Assert.Equal(["1B00", "1A00", ""], linhas.Select(l => l[Codigo]));
        Assert.Equal("BlockL1-1A0", linhas[2][Bloco]);
    }

    [Fact]
    public void ColunasForaDoPerfil_SaoIgnoradas()
    {
        var (linhas, _) = Converter(LinhaOms(code: "1A00", title: "t"));

        Assert.All(linhas, l => Assert.Equal(13, l.Length));
        Assert.DoesNotContain("English title", linhas[0]);
        Assert.DoesNotContain("browser", linhas[0]);
    }
}
