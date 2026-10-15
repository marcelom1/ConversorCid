using ConversorCid.Conversao;
using ConversorCid.Perfis;
using ConversorCid.Planilhas;
using static ConversorCid.Core.Tests.Apoio.DadosCid10;

namespace ConversorCid.Core.Tests;

public class PerfilCid10Tests
{
    private static readonly PerfilCid10 Perfil = new();

    // posições das colunas de saída
    private const int Codigo = 0, Bloco = 1, Titulo = 2, Tipo = 3, Capitulo = 4, Folha = 5, Grupo1 = 6, Grupo2 = 7, Grupo3 = 8,
        IntervaloInicio = 11, IntervaloFim = 12, Classificacao = 13, Referencia = 14;

    /// <summary>Uma linha de saída a partir dos 15 campos, para o teste não depender de contar barras.</summary>
    private static string L(params string[] campos)
    {
        Assert.Equal(15, campos.Length);
        return string.Join('|', campos);
    }

    private static (List<string> Linhas, ColetorAvisos Avisos) Converter(Dictionary<string, Planilha> entradas)
    {
        var avisos = new ColetorAvisos();
        var linhas = Perfil.Converter(entradas, avisos).Select(l => string.Join('|', l)).ToList();
        return (linhas, avisos);
    }

    private static (IReadOnlyList<string[]> Linhas, ColetorAvisos Avisos) ConverterCampos(Dictionary<string, Planilha> entradas)
    {
        var avisos = new ColetorAvisos();
        return (Perfil.Converter(entradas, avisos), avisos);
    }

    [Fact]
    public void ColunasDeSaida_AsTreze_DoCid11_SeguidasDeClassificationEReference()
    {
        var cid11 = new PerfilCid11().ColunasSaida;

        Assert.Equal(cid11, Perfil.ColunasSaida.Take(13));   // mesmo nome, posição e sentido: um parser lê os dois
        Assert.Equal(["Classification", "Reference"], Perfil.ColunasSaida.Skip(13));
    }

    [Fact]
    public void Entradas_SaoOsQuatroArquivosDoDatasus()
    {
        Assert.Equal(["capitulos", "grupos", "categorias", "subcategorias"], Perfil.Entradas.Select(e => e.Nome));
        Assert.Equal(
            ["CID-10-CAPITULOS.CSV", "CID-10-GRUPOS.CSV", "CID-10-CATEGORIAS.CSV", "CID-10-SUBCATEGORIAS.CSV"],
            Perfil.Entradas.Select(e => e.NomeDoArquivo));
        Assert.Equal("Cid10.csv", Perfil.ArquivoSaidaPadrao);
    }

    [Fact]
    public void ConjuntoPequeno_SaiEmPreOrdemComAHierarquiaCompleta()
    {
        var (linhas, avisos) = Converter(Mini());

        Assert.Equal(
        [
            L("", "", "Algumas doenças infecciosas e parasitárias", "chapter", "1", "FALSE", "", "", "", "", "", "A00", "B99", "", ""),
            L("", "A00-A09", "Doenças infecciosas intestinais", "block", "1", "FALSE", "", "", "", "", "", "A00", "A09", "", ""),
            L("A00", "", "Cólera", "category", "1", "FALSE", "A00-A09", "", "", "", "", "", "", "", ""),
            L("A000", "", "Cólera devida a Vibrio cholerae 01, biótipo cholerae", "subcategory", "1", "TRUE", "A00-A09", "", "", "", "", "", "", "", ""),
            L("A001", "", "Cólera devida a Vibrio cholerae 01, biótipo El Tor", "subcategory", "1", "TRUE", "A00-A09", "", "", "", "", "", "", "", ""),
            L("A09", "", "Diarréia e gastroenterite de origem infecciosa presumível", "category", "1", "TRUE", "A00-A09", "", "", "", "", "", "", "", ""),
            L("", "A15-A19", "Tuberculose", "block", "1", "FALSE", "", "", "", "", "", "A15", "A19", "", ""),
            L("A15", "", "Tuberculose respiratória", "category", "1", "FALSE", "A15-A19", "", "", "", "", "", "", "", ""),
            L("A150", "", "Tuberculose pulmonar confirmada", "subcategory", "1", "TRUE", "A15-A19", "", "", "", "", "", "", "", ""),
            L("", "", "Neoplasias [tumores]", "chapter", "2", "FALSE", "", "", "", "", "", "C00", "D48", "", ""),
            L("", "C00-C97", "Neoplasias malignas", "block", "2", "FALSE", "", "", "", "", "", "C00", "C97", "", ""),
            L("", "C00-C75", "Neoplasias malignas de localizações especificadas", "block", "2", "FALSE", "C00-C97", "", "", "", "", "C00", "C75", "", ""),
            L("", "C00-C14", "Neoplasias malignas do lábio, cavidade oral e faringe", "block", "2", "FALSE", "C00-C97", "C00-C75", "", "", "", "C00", "C14", "", ""),
            L("C00", "", "Neoplasia maligna do lábio", "category", "2", "FALSE", "C00-C97", "C00-C75", "C00-C14", "", "", "", "", "", ""),
            L("C000", "", "Neoplasia maligna do lábio superior, externo", "subcategory", "2", "TRUE", "C00-C97", "C00-C75", "C00-C14", "", "", "", "", "", ""),
        ], linhas);
        Assert.Empty(avisos.Avisos);
    }

    [Fact]
    public void CategoriaSemSubdivisao_SaiUmaLinhaSo_ComIsLeafTrue()
    {
        var (linhas, _) = ConverterCampos(Mini());

        var a09 = linhas.Where(l => l[Codigo] == "A09").ToList();
        var unica = Assert.Single(a09);
        Assert.Equal(("category", "TRUE"), (unica[Tipo], unica[Folha]));
    }

    [Fact]
    public void CategoriaFolha_HerdaClassificacaoEReferenciaDaSubcategoriaIrma()
    {
        var entradas = Entradas(
            Capitulos(("1", "A00", "B99", "Capítulo I - X")),
            Grupos(("B50", "B64", "Protozoários")),
            Categorias(I("B59", "Pneumocistose")),
            Subcategorias(I("B59", "Pneumocistose", "+", "J17.3*")));

        var (linhas, _) = ConverterCampos(entradas);

        var b59 = linhas.Single(l => l[Codigo] == "B59");
        Assert.Equal(("+", "J17.3*"), (b59[Classificacao], b59[Referencia]));
    }

    [Fact]
    public void CategoriaFolha_ComTituloDiferenteDaSubcategoria_AvisaEMantemOTituloDaCategoria()
    {
        var entradas = Entradas(
            Capitulos(("1", "A00", "B99", "Capítulo I - X")),
            Grupos(("A00", "A09", "G")),
            Categorias(I("A09", "Título da categoria")),
            Subcategorias(I("A09", "Título da subcategoria")));

        var (linhas, avisos) = ConverterCampos(entradas);

        Assert.Equal("Título da categoria", linhas.Single(l => l[Codigo] == "A09")[Titulo]);
        Assert.Contains("A09", Assert.Single(avisos.Avisos).Mensagem);
    }

    [Fact]
    public void ClassificacaoEReferencia_VaoParaAsDuasUltimasColunas()
    {
        var entradas = Entradas(
            Capitulos(("1", "A00", "B99", "Capítulo I - X")),
            Grupos(("A00", "A09", "G")),
            Categorias(I("A06", "Amebíase")),
            Subcategorias(I("A065", "Abscesso amebiano do pulmão", "+", "J99.8*")));

        var (linhas, _) = ConverterCampos(entradas);

        var a065 = linhas.Single(l => l[Codigo] == "A065");
        Assert.Equal(("+", "J99.8*"), (a065[Classificacao], a065[Referencia]));
    }

    [Fact]
    public void GruposAninhados_AcategoriaListaTodosOsGrupos_DoMaisExternoParaOMaisInterno()
    {
        var (linhas, _) = ConverterCampos(Mini());

        var c00 = linhas.Single(l => l[Codigo] == "C00");
        Assert.Equal(("C00-C97", "C00-C75", "C00-C14"), (c00[Grupo1], c00[Grupo2], c00[Grupo3]));
    }

    [Fact]
    public void GrupoDeUmCodigoSo_ContemACategoriaDeMesmoCodigo()
    {
        // regressão: a faixa "U99-U99" é idêntica à "faixa" da categoria U99 e deve contê-la
        var entradas = Entradas(
            Capitulos(("22", "U04", "U99", "Capítulo XXII - Códigos para propósitos especiais")),
            Grupos(("U99", "U99", "CID 10ª Revisão não disponível")),
            Categorias(I("U99", "CID 10ª Revisão não disponível")),
            Subcategorias(I("U99", "CID 10ª Revisão não disponível")));

        var (linhas, _) = ConverterCampos(entradas);

        Assert.Equal("U99-U99", linhas.Single(l => l[Codigo] == "U99")[Grupo1]);
        Assert.Equal("", linhas.Single(l => l[Bloco] == "U99-U99")[Grupo1]);   // o grupo não é ancestral de si mesmo
    }

    [Fact]
    public void GruposComOMesmoInicio_ExternoAntesDoInterno_EOGrupoAntesDaCategoria()
    {
        var (linhas, _) = ConverterCampos(Mini());

        var ordem = linhas.Where(l => l[Capitulo] == "2").Select(l => l[Bloco].Length > 0 ? l[Bloco] : l[Codigo]).ToList();
        Assert.Equal(["", "C00-C97", "C00-C75", "C00-C14", "C00", "C000"], ordem);
    }

    [Fact]
    public void Capitulos_SaemEmOrdemNumerica_EAoFimOQueTemCodigoMaisBaixo()
    {
        // o capítulo 22 (U04-U99) vem depois do 21 (Z00-Z99) mesmo com letra "menor"
        var entradas = Entradas(
            Capitulos(("22", "U04", "U99", "Capítulo XXII - Especiais"), ("21", "Z00", "Z99", "Capítulo XXI - Fatores"), ("2", "C00", "D48", "Capítulo II - Neoplasias")),
            Grupos(("U04", "U04", "G1"), ("Z00", "Z13", "G2"), ("C00", "C97", "G3")),
            Categorias(I("U04", "A"), I("Z00", "B"), I("C00", "C")),
            Subcategorias(I("U04", "A"), I("Z00", "B"), I("C00", "C")));

        var (linhas, _) = ConverterCampos(entradas);

        Assert.Equal(["2", "21", "22"], linhas.Where(l => l[Tipo] == "chapter").Select(l => l[Capitulo]));
    }

    [Theory]
    [InlineData("Capítulo I - Algumas doenças", "Algumas doenças")]
    [InlineData("Capítulo III  - Doenças do sangue", "Doenças do sangue")]
    [InlineData("Capítulo XXII - Códigos para propósitos especiais", "Códigos para propósitos especiais")]
    [InlineData("Capitulo IV - Sem acento", "Sem acento")]
    [InlineData("Neoplasias", "Neoplasias")]
    public void TituloDeCapitulo_PerdeOPrefixoComNumeral(string entrada, string esperado)
    {
        var entradas = Entradas(
            Capitulos(("1", "A00", "B99", entrada)), Grupos(), Categorias(), Subcategorias());

        var (linhas, _) = ConverterCampos(entradas);

        Assert.Equal(esperado, linhas.Single()[Titulo]);
    }

    [Fact]
    public void Intervalos_SaemNosCapitulosENosGrupos_EMaisEmNenhumaOutraLinha()
    {
        var (linhas, _) = ConverterCampos(Mini());

        Assert.All(linhas.Where(l => l[Tipo] is "category" or "subcategory"), l => Assert.Equal(("", ""), (l[IntervaloInicio], l[IntervaloFim])));
        Assert.All(linhas.Where(l => l[Tipo] is "chapter" or "block"), l => Assert.NotEmpty(l[IntervaloInicio]));
        Assert.Equal(("A00", "A09"), (linhas[1][IntervaloInicio], linhas[1][IntervaloFim]));
    }

    [Fact]
    public void SeparadorNoTitulo_TrocaPorEspacoEAvisaComALinhaDoArquivo()
    {
        var entradas = Mini();
        entradas["subcategorias"] = Subcategorias(
            I("A000", "Cólera"),
            I("A001", "Texto|com barra"),
            I("A09", "Diarréia e gastroenterite de origem infecciosa presumível"));

        var (linhas, avisos) = ConverterCampos(entradas);

        Assert.Equal("Texto com barra", linhas.Single(l => l[Codigo] == "A001")[Titulo]);
        var aviso = Assert.Single(avisos.Avisos);
        Assert.Equal(3, aviso.Linha);
        Assert.Contains("A001", aviso.Mensagem);
    }

    [Fact]
    public void TituloVazio_GravaEmBrancoEAvisa()
    {
        var entradas = Mini();
        entradas["subcategorias"] = Subcategorias(I("A000", ""), I("A09", "Diarréia e gastroenterite de origem infecciosa presumível"));

        var (linhas, avisos) = ConverterCampos(entradas);

        Assert.Equal("", linhas.Single(l => l[Codigo] == "A000")[Titulo]);
        Assert.Contains("A000", Assert.Single(avisos.Avisos).Mensagem);
    }

    [Fact]
    public void SubcategoriaSemCategoria_EIgnoradaComAviso()
    {
        var entradas = Mini();
        entradas["subcategorias"] = Subcategorias(I("A000", "Ok"), I("A09", "Diarréia e gastroenterite de origem infecciosa presumível"), I("A990", "Órfã"));

        var (linhas, avisos) = ConverterCampos(entradas);

        Assert.DoesNotContain(linhas, l => l[Codigo] == "A990");
        var aviso = Assert.Single(avisos.Avisos);
        Assert.Contains("A990", aviso.Mensagem);
        Assert.Contains("A99", aviso.Mensagem);
    }

    [Fact]
    public void CategoriaForaDeQualquerCapitulo_EIgnoradaComAviso()
    {
        var entradas = Mini();
        entradas["categorias"] = Categorias(I("A00", "Cólera"), I("A09", "Diarréia e gastroenterite de origem infecciosa presumível"), I("A15", "T"), I("C00", "N"), I("W99", "Fora"));

        var (linhas, avisos) = ConverterCampos(entradas);

        Assert.DoesNotContain(linhas, l => l[Codigo] == "W99");
        Assert.Contains("W99", Assert.Single(avisos.Avisos).Mensagem);
    }

    [Fact]
    public void GrupoQueAtravessaCapitulos_EIgnoradoComAviso()
    {
        var entradas = Mini();
        entradas["grupos"] = Grupos(("A00", "A09", "Ok"), ("B90", "C10", "Atravessa"));

        var (linhas, avisos) = ConverterCampos(entradas);

        Assert.DoesNotContain(linhas, l => l[Bloco] == "B90-C10");
        Assert.Contains("B90-C10", Assert.Single(avisos.Avisos).Mensagem);
    }

    [Fact]
    public void CodigoRepetido_MantemOPrimeiroEAvisa()
    {
        var entradas = Mini();
        entradas["categorias"] = Categorias(I("A00", "Primeira"), I("A00", "Segunda"), I("A09", "Diarréia e gastroenterite de origem infecciosa presumível"), I("A15", "T"), I("C00", "N"));

        var (linhas, avisos) = ConverterCampos(entradas);

        Assert.Equal("Primeira", linhas.Single(l => l[Codigo] == "A00" && l[Tipo] == "category")[Titulo]);
        Assert.Contains("repetido", Assert.Single(avisos.Avisos).Mensagem);
    }

    [Fact]
    public void TitulosMaioresQueOLimiteDoRh_GeramUmAvisoAgregado()
    {
        var longo = new string('x', 191);
        var entradas = Mini();
        entradas["subcategorias"] = Subcategorias(I("A000", longo), I("A001", longo), I("A09", "Diarréia e gastroenterite de origem infecciosa presumível"), I("A150", "ok"), I("C000", "ok"));

        var (_, avisos) = ConverterCampos(entradas);

        var aviso = Assert.Single(avisos.Avisos);
        Assert.Equal(0, aviso.Linha);
        Assert.Contains("2 título(s) de subcategoria", aviso.Mensagem);
        Assert.Contains("190", aviso.Mensagem);
        Assert.Contains("A000, A001", aviso.Mensagem);
    }

    [Fact]
    public void TituloNoLimiteExato_NaoAvisa()
    {
        var entradas = Mini();
        entradas["subcategorias"] = Subcategorias(I("A000", new string('x', 190)), I("A09", "Diarréia e gastroenterite de origem infecciosa presumível"));

        var (_, avisos) = ConverterCampos(entradas);

        Assert.Empty(avisos.Avisos);
    }

    [Fact]
    public void NumeroDeCapituloInvalido_LancaPlanilhaInvalida()
    {
        var entradas = Mini();
        entradas["capitulos"] = Capitulos(("I", "A00", "B99", "Capítulo I - X"));

        var ex = Assert.Throws<PlanilhaInvalidaException>(() => Perfil.Converter(entradas, new ColetorAvisos()));

        Assert.Contains("NUMCAP", ex.Message);
    }

    [Fact]
    public void CodigosEmMinusculas_SaoNormalizadosParaMaiusculas()
    {
        var entradas = Entradas(
            Capitulos(("1", "a00", "b99", "Capítulo I - X")),
            Grupos(("a00", "a09", "G")),
            Categorias(I("a00", "Cólera")),
            Subcategorias(I("a000", "Cólera tipo 0")));

        var (linhas, avisos) = ConverterCampos(entradas);

        Assert.Equal(["", "A00-A09", "A00", "A000"], linhas.Select(l => l[Bloco].Length > 0 ? l[Bloco] : l[Codigo]));
        Assert.Empty(avisos.Avisos);
    }

    [Fact]
    public void TodaLinhaTemQuinzeCamposESemSeparadorNemQuebraDeLinha()
    {
        var (linhas, _) = ConverterCampos(Mini());

        Assert.All(linhas, l =>
        {
            Assert.Equal(15, l.Length);
            Assert.All(l, campo => Assert.Equal(-1, campo.IndexOfAny(['|', '\r', '\n'])));
        });
    }
}
