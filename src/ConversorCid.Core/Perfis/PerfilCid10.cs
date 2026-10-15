using ConversorCid.Conversao;
using ConversorCid.Planilhas;

namespace ConversorCid.Perfis;

/// <summary>
/// CID-10 a partir dos quatro CSVs que o DATASUS publica (capítulos, grupos, categorias e subcategorias),
/// gerando um único CSV no molde do CID-11: as 13 primeiras colunas têm o mesmo nome, posição e sentido do
/// <see cref="PerfilCid11"/>, e <c>Classification</c> e <c>Reference</c> vêm no fim.
/// <list type="bullet">
/// <item><c>ClassKind</c>: <c>chapter</c> (22), <c>block</c> (as faixas de <c>GRUPOS</c>, como "A00-A09", identificadas em
/// <c>BlockId</c>), <c>category</c> (3 caracteres, "A00") e <c>subcategory</c> (4 caracteres, "A000").</item>
/// <item>As linhas saem em pré-ordem, como no CID-11: o capítulo, depois seus grupos, categorias e subcategorias
/// em ordem de código, com cada grupo antes do que ele contém.</item>
/// <item><c>Grouping1..5</c> são as faixas que contêm a linha, da mais externa para a mais interna (o DATASUS aninha
/// até 3 níveis). O RH só guarda a mais externa em <c>MS_CIDCATEGORIADOENCAS</c>.</item>
/// <item><c>Intervalos Inicio/Fim</c> saem nos capítulos e nos grupos (o CID-11 só os usa nos capítulos): no RH
/// são <c>MS_CIDGRUPOS.INTERVALO</c> e <c>MS_CIDCATEGORIADOENCAS.CATEGORIAINICIO/FIM</c>.</item>
/// <item>Categoria sem subdivisão aparece nos dois arquivos, com o mesmo título (o RH as guarda como categoria e como
/// CID). Sai uma linha só, <c>category</c> com <c>isLeaf</c> TRUE; o importador a trata como CID, como faz com o CID-11.</item>
/// <item><c>Classification</c> (<c>+</c> etiologia, <c>*</c> manifestação) e <c>Reference</c> (o código par) vêm do
/// DATASUS; o RH legado os embutia no código e na descrição.</item>
/// </list>
/// Fora do CSV ficam <c>RESTRSEXO</c>, <c>CAUSAOBITO</c>, <c>EXCLUIDOS</c> e <c>DESCRABREV</c>, que o RH não modela.
/// </summary>
public sealed class PerfilCid10 : IPerfilConversao
{
    private const string EntCapitulos = "capitulos";
    private const string EntGrupos = "grupos";
    private const string EntCategorias = "categorias";
    private const string EntSubcategorias = "subcategorias";

    private const string TipoCapitulo = "chapter";
    private const string TipoGrupo = "block";
    private const string TipoCategoria = "category";
    private const string TipoSubcategoria = "subcategory";

    private const int MaximoDeAgrupamentos = 5;

    // Tamanho das colunas de destino no RH, para avisar do que o importador vai truncar.
    private const int LimiteCapitulo = 150;   // MS_CIDGRUPOS.DESCRICAO
    private const int LimiteGrupo = 200;      // MS_CIDCATEGORIADOENCAS.DESCRICAO
    private const int LimiteCategoria = 200;  // MS_CIDCATEGORIAS.DESCRICAO
    private const int LimiteCid = 190;        // MS_CIDS.DESCRICAO

    public string Nome => "cid10";

    public string Descricao => "CID-10 (DATASUS): pasta com os CSVs CID-10-CAPITULOS, -GRUPOS, -CATEGORIAS e -SUBCATEGORIAS";

    public string ArquivoSaidaPadrao => "Cid10.csv";

    public OndeBaixar? OndeBaixar { get; } = new(
        Url: "http://www2.datasus.gov.br/cid10/V2008/cid10.htm",
        Dica: "Dica: com o site aberto, em “Tabelas da CID-10” baixe os “arquivos em formato CSV”, descompacte o .zip e escolha a pasta aqui.",
        LinksDaDica: [new LinkDaDica("Tabelas da CID-10", "http://www2.datasus.gov.br/cid10/V2008/download.htm")]);

    public IReadOnlyList<EntradaDoPerfil> Entradas { get; } =
    [
        Entrada(EntCapitulos, "capítulos", "CID-10-CAPITULOS.CSV", "NUMCAP", "CATINIC", "CATFIM", "DESCRICAO"),
        Entrada(EntGrupos, "grupos", "CID-10-GRUPOS.CSV", "CATINIC", "CATFIM", "DESCRICAO"),
        Entrada(EntCategorias, "categorias", "CID-10-CATEGORIAS.CSV", "CAT", "CLASSIF", "DESCRICAO", "REFER"),
        Entrada(EntSubcategorias, "subcategorias", "CID-10-SUBCATEGORIAS.CSV", "SUBCAT", "CLASSIF", "DESCRICAO", "REFER"),
    ];

    public IReadOnlyList<string> ColunasSaida { get; } =
    [
        "Code", "BlockId", "Title", "ClassKind", "ChapterNo", "isLeaf",
        "Grouping1", "Grouping2", "Grouping3", "Grouping4", "Grouping5",
        "Intervalos Inicio", "Intervalos Fim", "Classification", "Reference",
    ];

    private static EntradaDoPerfil Entrada(string nome, string descricao, string arquivo, params string[] colunas) =>
        new(nome, $"{descricao} ({arquivo})", colunas, new CsvPlanilhaLeitor(';'), arquivo);

    public IReadOnlyList<string[]> Converter(IReadOnlyDictionary<string, Planilha> entradas, ColetorAvisos avisos)
    {
        var capitulos = LerCapitulos(entradas[EntCapitulos], avisos);
        var grupos = LerGrupos(entradas[EntGrupos], avisos);
        var categorias = LerCategorias(entradas[EntCategorias], avisos);
        var subcategorias = LerSubcategorias(entradas[EntSubcategorias], avisos);

        var folhas = subcategorias.Where(s => s.Codigo.Length == 3).Select(s => s.Codigo).ToHashSet(StringComparer.Ordinal);
        var longos = new Dictionary<string, List<string>>();
        var nos = new List<No>(capitulos.Count + grupos.Count + categorias.Count + subcategorias.Count);

        foreach (var capitulo in capitulos)
        {
            Verificar(longos, "capítulo", LimiteCapitulo, capitulo.Numero, capitulo.Titulo);
            nos.Add(new No(int.Parse(capitulo.Numero), string.Empty, -1, string.Empty,
                Linha(string.Empty, string.Empty, capitulo.Titulo, TipoCapitulo, capitulo.Numero, false, [], capitulo.Inicio, capitulo.Fim, string.Empty, string.Empty)));
        }

        foreach (var grupo in grupos)
        {
            var capitulo = CapituloDe(capitulos, grupo.Inicio);
            if (capitulo is null || CapituloDe(capitulos, grupo.Fim) != capitulo)
            {
                avisos.Adicionar(grupo.Linha, $"grupo {grupo.Id}: a faixa não cabe em um único capítulo; grupo ignorado.");
                continue;
            }

            Verificar(longos, "grupo", LimiteGrupo, grupo.Id, grupo.Titulo);
            var ancestrais = AncestraisDe(grupos, grupo.Inicio, grupo.Fim, ignorar: grupo);
            nos.Add(new No(int.Parse(capitulo.Numero), grupo.Inicio, 0, grupo.Fim,
                Linha(string.Empty, grupo.Id, grupo.Titulo, TipoGrupo, capitulo.Numero, false, Identificadores(ancestrais, $"grupo {grupo.Id}", grupo.Linha, avisos), grupo.Inicio, grupo.Fim, string.Empty, string.Empty)));
        }

        var cadeiaPorCategoria = new Dictionary<string, (Capitulo Capitulo, string[] Grupos)>(StringComparer.Ordinal);
        foreach (var categoria in categorias)
        {
            var capitulo = CapituloDe(capitulos, categoria.Codigo);
            if (capitulo is null)
            {
                avisos.Adicionar(categoria.Linha, $"categoria {categoria.Codigo}: não pertence a nenhum capítulo; ignorada.");
                continue;
            }

            var cadeia = Identificadores(AncestraisDe(grupos, categoria.Codigo, categoria.Codigo, ignorar: null), $"categoria {categoria.Codigo}", categoria.Linha, avisos);
            cadeiaPorCategoria[categoria.Codigo] = (capitulo, cadeia);

            // Categoria sem subdivisão: o título e a classificação do CID vêm da subcategoria de mesmo código.
            var folha = folhas.Contains(categoria.Codigo);
            var subcategoriaIrma = folha ? subcategorias.First(s => s.Codigo == categoria.Codigo) : null;
            if (subcategoriaIrma is not null && !string.Equals(subcategoriaIrma.Titulo, categoria.Titulo, StringComparison.Ordinal))
                avisos.Adicionar(categoria.Linha, $"categoria {categoria.Codigo}: o título difere do da subcategoria de mesmo código; mantido o da categoria.");

            Verificar(longos, folha ? "categoria sem subdivisão" : "categoria", folha ? Math.Min(LimiteCategoria, LimiteCid) : LimiteCategoria, categoria.Codigo, categoria.Titulo);

            nos.Add(new No(int.Parse(capitulo.Numero), categoria.Codigo, 1, string.Empty,
                Linha(categoria.Codigo, string.Empty, categoria.Titulo, TipoCategoria, capitulo.Numero, folha, cadeia, string.Empty, string.Empty,
                    categoria.Classificacao.Length > 0 ? categoria.Classificacao : subcategoriaIrma?.Classificacao ?? string.Empty,
                    categoria.Referencia.Length > 0 ? categoria.Referencia : subcategoriaIrma?.Referencia ?? string.Empty)));
        }

        foreach (var subcategoria in subcategorias.Where(s => s.Codigo.Length != 3))
        {
            if (subcategoria.Codigo.Length < 3 || !cadeiaPorCategoria.TryGetValue(subcategoria.Codigo[..3], out var origem))
            {
                avisos.Adicionar(subcategoria.Linha, $"subcategoria {subcategoria.Codigo}: a categoria {subcategoria.Codigo[..Math.Min(3, subcategoria.Codigo.Length)]} não existe; ignorada.");
                continue;
            }

            Verificar(longos, "subcategoria", LimiteCid, subcategoria.Codigo, subcategoria.Titulo);
            nos.Add(new No(int.Parse(origem.Capitulo.Numero), subcategoria.Codigo, 2, string.Empty,
                Linha(subcategoria.Codigo, string.Empty, subcategoria.Titulo, TipoSubcategoria, origem.Capitulo.Numero, true, origem.Grupos, string.Empty, string.Empty, subcategoria.Classificacao, subcategoria.Referencia)));
        }

        foreach (var (tipo, itens) in longos)
        {
            var limite = tipo switch { "capítulo" => LimiteCapitulo, "grupo" => LimiteGrupo, "subcategoria" => LimiteCid, "categoria sem subdivisão" => LimiteCid, _ => LimiteCategoria };
            avisos.Adicionar($"{itens.Count} título(s) de {tipo} passam de {limite} caracteres e serão truncados na importação. Primeiros: {string.Join(", ", itens.Take(5))}.");
        }

        return nos
            .OrderBy(n => n.Capitulo)
            .ThenBy(n => n.Inicio, StringComparer.Ordinal)
            .ThenBy(n => n.Ordem)
            .ThenByDescending(n => n.Fim, StringComparer.Ordinal)
            .Select(n => n.Linha)
            .ToList();
    }

    private static string[] Linha(
        string codigo, string bloco, string titulo, string tipo, string capitulo, bool folha,
        IReadOnlyList<string> agrupamentos, string inicio, string fim, string classificacao, string referencia)
    {
        var linha = new string[15];
        linha[0] = codigo;
        linha[1] = bloco;
        linha[2] = titulo;
        linha[3] = tipo;
        linha[4] = capitulo;
        linha[5] = folha ? "TRUE" : "FALSE";
        for (var i = 0; i < MaximoDeAgrupamentos; i++)
            linha[6 + i] = i < agrupamentos.Count ? agrupamentos[i] : string.Empty;
        linha[11] = inicio;
        linha[12] = fim;
        linha[13] = classificacao;
        linha[14] = referencia;
        return linha;
    }

    private static string[] Identificadores(IReadOnlyList<Grupo> ancestrais, string identificacao, int linha, ColetorAvisos avisos)
    {
        if (ancestrais.Count > MaximoDeAgrupamentos)
            avisos.Adicionar(linha, $"{identificacao}: está dentro de {ancestrais.Count} grupos e só cabem {MaximoDeAgrupamentos}; mantidos os mais externos.");

        return ancestrais.Take(MaximoDeAgrupamentos).Select(g => g.Id).ToArray();
    }

    /// <summary>
    /// As faixas que contêm a faixa dada, da mais externa para a mais interna. Um grupo de um código só ("U99-U99")
    /// contém a categoria U99, mesmo com a faixa idêntica; só o próprio grupo fica de fora, e não há dois grupos
    /// com a mesma faixa porque os repetidos são descartados na leitura.
    /// </summary>
    private static List<Grupo> AncestraisDe(IReadOnlyList<Grupo> grupos, string inicio, string fim, Grupo? ignorar) =>
        grupos
            .Where(g => !ReferenceEquals(g, ignorar)
                && string.CompareOrdinal(g.Inicio, inicio) <= 0
                && string.CompareOrdinal(fim, g.Fim) <= 0)
            .OrderBy(g => g.Inicio, StringComparer.Ordinal)
            .ThenByDescending(g => g.Fim, StringComparer.Ordinal)
            .ToList();

    private static Capitulo? CapituloDe(IReadOnlyList<Capitulo> capitulos, string codigo) =>
        capitulos.FirstOrDefault(c => string.CompareOrdinal(c.Inicio, codigo) <= 0 && string.CompareOrdinal(codigo, c.Fim) <= 0);

    private static void Verificar(Dictionary<string, List<string>> longos, string tipo, int limite, string codigo, string titulo)
    {
        if (titulo.Length <= limite)
            return;

        if (!longos.TryGetValue(tipo, out var lista))
            longos[tipo] = lista = [];
        lista.Add(codigo);
    }

    private static List<Capitulo> LerCapitulos(Planilha tabela, ColetorAvisos avisos)
    {
        var lista = new List<Capitulo>();
        foreach (var linha in tabela.Linhas)
        {
            var numero = Transformacoes.NormalizarNumeroDeCapitulo(linha["NUMCAP"]);
            if (numero.Length == 0 || !numero.All(char.IsAsciiDigit))
                throw new PlanilhaInvalidaException($"Capítulos, linha {linha.Numero}: NUMCAP '{linha["NUMCAP"]}' não é um número.");

            var identificacao = $"capítulo {numero}";
            var titulo = Transformacoes.RemoverPrefixoDeCapitulo(LeituraDeCampos.Limpo(linha, "DESCRICAO", identificacao, avisos));
            AvisarTituloVazio(titulo, linha.Numero, identificacao, avisos);
            lista.Add(new Capitulo(numero, Codigo(linha, "CATINIC"), Codigo(linha, "CATFIM"), titulo, linha.Numero));
        }

        return [.. lista.OrderBy(c => int.Parse(c.Numero))];
    }

    private static List<Grupo> LerGrupos(Planilha tabela, ColetorAvisos avisos)
    {
        var lista = new List<Grupo>();
        var vistos = new HashSet<string>(StringComparer.Ordinal);
        foreach (var linha in tabela.Linhas)
        {
            var grupo = new Grupo(Codigo(linha, "CATINIC"), Codigo(linha, "CATFIM"), string.Empty, linha.Numero);
            var identificacao = $"grupo {grupo.Id}";
            if (!vistos.Add(grupo.Id))
            {
                avisos.Adicionar(linha.Numero, $"{identificacao}: faixa repetida; mantida a primeira.");
                continue;
            }

            var titulo = LeituraDeCampos.Limpo(linha, "DESCRICAO", identificacao, avisos);
            AvisarTituloVazio(titulo, linha.Numero, identificacao, avisos);
            lista.Add(grupo with { Titulo = titulo });
        }

        return lista;
    }

    private static List<Categoria> LerCategorias(Planilha tabela, ColetorAvisos avisos) =>
        LerCodigos(tabela, avisos, "CAT", "categoria");

    private static List<Categoria> LerSubcategorias(Planilha tabela, ColetorAvisos avisos) =>
        LerCodigos(tabela, avisos, "SUBCAT", "subcategoria");

    private static List<Categoria> LerCodigos(Planilha tabela, ColetorAvisos avisos, string colunaCodigo, string rotulo)
    {
        var lista = new List<Categoria>();
        var vistos = new HashSet<string>(StringComparer.Ordinal);
        foreach (var linha in tabela.Linhas)
        {
            var codigo = Codigo(linha, colunaCodigo);
            var identificacao = $"{rotulo} {codigo}";
            if (codigo.Length == 0 || !vistos.Add(codigo))
            {
                avisos.Adicionar(linha.Numero, codigo.Length == 0 ? $"{rotulo} sem código; ignorada." : $"{identificacao}: código repetido; mantida a primeira.");
                continue;
            }

            var titulo = LeituraDeCampos.Limpo(linha, "DESCRICAO", identificacao, avisos);
            AvisarTituloVazio(titulo, linha.Numero, identificacao, avisos);
            lista.Add(new Categoria(
                codigo,
                LeituraDeCampos.Limpo(linha, "CLASSIF", identificacao, avisos),
                titulo,
                LeituraDeCampos.Limpo(linha, "REFER", identificacao, avisos),
                linha.Numero));
        }

        return lista;
    }

    private static string Codigo(LinhaPlanilha linha, string coluna) => linha[coluna].Trim().ToUpperInvariant();

    private static void AvisarTituloVazio(string titulo, int linha, string identificacao, ColetorAvisos avisos)
    {
        if (titulo.Length == 0)
            avisos.Adicionar(linha, $"{identificacao}: sem descrição no arquivo; gravado em branco.");
    }

    private sealed record Capitulo(string Numero, string Inicio, string Fim, string Titulo, int Linha);

    private sealed record Grupo(string Inicio, string Fim, string Titulo, int Linha)
    {
        public string Id => $"{Inicio}-{Fim}";
    }

    /// <summary>Categoria (3 caracteres) ou subcategoria (4): mesmas colunas no DATASUS.</summary>
    private sealed record Categoria(string Codigo, string Classificacao, string Titulo, string Referencia, int Linha);

    /// <summary>Uma linha do CSV com a chave da ordenação final.</summary>
    private sealed record No(int Capitulo, string Inicio, int Ordem, string Fim, string[] Linha);
}
