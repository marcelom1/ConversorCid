using ConversorCid.Conversao;
using ConversorCid.Planilhas;
using ConversorCid.Saida;

namespace ConversorCid.Perfis;

/// <summary>
/// CID-11 a partir da planilha "SimpleTabulation ICD-11 MMS" da OMS em português.
/// <list type="bullet">
/// <item>Saem 11 colunas da planilha (as URIs, o título em inglês, notas e o restante não saem) mais
/// <c>Intervalos Inicio</c> e <c>Intervalos Fim</c>, que não existem na planilha.</item>
/// <item><c>Title</c> perde os marcadores de nível ("- - - "), <c>ChapterNo</c> perde o zero à esquerda e
/// <c>isLeaf</c> sai como TRUE/FALSE.</item>
/// <item>Só as linhas de capítulo (<c>ClassKind</c> = chapter) preenchem os intervalos: o menor e o maior
/// código de categoria do capítulo, desconsiderando as subdivisões com ponto (1A00.0).</item>
/// </list>
/// </summary>
public sealed class PerfilCid11 : IPerfilConversao
{
    private const string ColCodigo = "Code";
    private const string ColBloco = "BlockId";
    private const string ColTitulo = "Title";
    private const string ColTipo = "ClassKind";
    private const string ColCapitulo = "ChapterNo";
    private const string ColFolha = "isLeaf";

    private const string TipoCapitulo = "chapter";
    private const string TipoBloco = "block";
    private const string TipoCategoria = "category";

    private static readonly string[] Agrupamentos = ["Grouping1", "Grouping2", "Grouping3", "Grouping4", "Grouping5"];

    public string Nome => "cid11";

    public string Descricao => "CID-11 (OMS): planilha SimpleTabulation ICD-11 MMS em português";

    public string ArquivoSaidaPadrao => "Cid11.csv";

    public OndeBaixar? OndeBaixar { get; } = new(
        Url: "https://icd.who.int/browse/2026-01/mms/pt",
        Dica: "Dica: no menu “Info” → “Arquivo em planilha”.",
        LinksDaDica: []);

    public IReadOnlyList<EntradaDoPerfil> Entradas { get; } =
    [
        new EntradaDoPerfil(
            Nome: "planilha",
            Descricao: "planilha SimpleTabulation ICD-11 MMS (.xlsx)",
            ColunasObrigatorias: [ColCodigo, ColBloco, ColTitulo, ColTipo, ColCapitulo, ColFolha, .. Agrupamentos],
            Leitor: new ExcelPlanilhaLeitor()),
    ];

    public IReadOnlyList<string> ColunasSaida { get; } =
        [ColCodigo, ColBloco, ColTitulo, ColTipo, ColCapitulo, ColFolha, .. Agrupamentos, "Intervalos Inicio", "Intervalos Fim"];

    public IReadOnlyList<string[]> Converter(IReadOnlyDictionary<string, Planilha> entradas, ColetorAvisos avisos) =>
        Converter(entradas["planilha"], avisos);

    public IReadOnlyList<string[]> Converter(Planilha planilha, ColetorAvisos avisos)
    {
        var intervalos = CalcularIntervalosDosCapitulos(planilha);
        var saida = new List<string[]>(planilha.Linhas.Count);

        foreach (var linha in planilha.Linhas)
        {
            var identificacao = Identificar(linha);
            string Campo(string coluna) => LeituraDeCampos.Limpo(linha, coluna, identificacao, avisos);

            var tipo = Campo(ColTipo);
            if (!tipo.Equals(TipoCapitulo, StringComparison.OrdinalIgnoreCase) &&
                !tipo.Equals(TipoBloco, StringComparison.OrdinalIgnoreCase) &&
                !tipo.Equals(TipoCategoria, StringComparison.OrdinalIgnoreCase))
            {
                avisos.Adicionar(linha.Numero, $"{identificacao}: ClassKind '{tipo}' não é chapter, block nem category.");
            }

            var titulo = Transformacoes.RemoverMarcadoresDeNivel(Campo(ColTitulo));
            if (titulo.Length == 0)
                avisos.Adicionar(linha.Numero, $"{identificacao}: sem título em português (Title vazio na planilha); gravado em branco.");

            var capitulo = Transformacoes.NormalizarNumeroDeCapitulo(Campo(ColCapitulo));

            var folhaOriginal = Campo(ColFolha);
            if (!Transformacoes.TentarNormalizarBooleano(folhaOriginal, out var folha))
                avisos.Adicionar(linha.Numero, $"{identificacao}: isLeaf '{folhaOriginal}' não é TRUE nem FALSE; mantido como está.");

            var inicio = string.Empty;
            var fim = string.Empty;
            if (tipo.Equals(TipoCapitulo, StringComparison.OrdinalIgnoreCase))
            {
                if (intervalos.TryGetValue(capitulo, out var intervalo))
                    (inicio, fim) = intervalo;
                else
                    avisos.Adicionar(linha.Numero, $"{identificacao}: o capítulo '{capitulo}' não tem categorias na planilha; Intervalos em branco.");
            }

            saida.Add(
            [
                Campo(ColCodigo), Campo(ColBloco), titulo, tipo, capitulo, folha,
                .. Agrupamentos.Select(Campo),
                inicio, fim,
            ]);
        }

        return saida;
    }

    private static Dictionary<string, (string Inicio, string Fim)> CalcularIntervalosDosCapitulos(Planilha planilha)
    {
        var intervalos = new Dictionary<string, (string Inicio, string Fim)>(StringComparer.Ordinal);

        foreach (var linha in planilha.Linhas)
        {
            if (!linha[ColTipo].Trim().Equals(TipoCategoria, StringComparison.OrdinalIgnoreCase))
                continue;

            var codigo = linha[ColCodigo].Trim();
            if (codigo.Length == 0 || codigo.Contains('.'))
                continue;

            var capitulo = Transformacoes.NormalizarNumeroDeCapitulo(linha[ColCapitulo]);
            if (!intervalos.TryGetValue(capitulo, out var atual))
            {
                intervalos[capitulo] = (codigo, codigo);
                continue;
            }

            intervalos[capitulo] =
            (
                string.CompareOrdinal(codigo, atual.Inicio) < 0 ? codigo : atual.Inicio,
                string.CompareOrdinal(codigo, atual.Fim) > 0 ? codigo : atual.Fim
            );
        }

        return intervalos;
    }

    /// <summary>Como o usuário reconhece a linha: o código; o bloco quando não há código; senão o tipo e o capítulo.</summary>
    private static string Identificar(LinhaPlanilha linha)
    {
        var codigo = linha[ColCodigo].Trim();
        if (codigo.Length > 0)
            return codigo;

        var bloco = linha[ColBloco].Trim();
        return bloco.Length > 0 ? bloco : $"{linha[ColTipo].Trim()} {linha[ColCapitulo].Trim()}".Trim();
    }
}
