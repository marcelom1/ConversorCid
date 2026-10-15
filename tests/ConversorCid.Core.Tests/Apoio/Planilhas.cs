using ClosedXML.Excel;
using ConversorCid.Planilhas;

namespace ConversorCid.Core.Tests.Apoio;

internal static class Planilhas
{
    /// <summary>As 22 colunas da planilha da OMS, na ordem original. Só 11 delas entram no CSV do CID-11.</summary>
    public static readonly string[] CabecalhoOms =
    [
        "Foundation URI", "Linearization URI", "Code", "BlockId", "TitleEN", "Title", "ClassKind", "DepthInKind",
        "IsResidual", "ChapterNo", "BrowserLink", "isLeaf", "Primary tabulation", "Grouping1", "Grouping2",
        "Grouping3", "Grouping4", "Grouping5", "CodingNote", "Parent", "Version:2026 Jan 17 - 05:30 UTC", string.Empty,
    ];

    /// <summary>Linha no layout da OMS; o que não for informado fica vazio (o capítulo, quando omitido, é "01").</summary>
    public static object?[] LinhaOms(
        string code = "", string blockId = "", string title = "", string classKind = "category",
        object? chapterNo = null, string isLeaf = "True",
        string g1 = "", string g2 = "", string g3 = "", string g4 = "", string g5 = "") =>
    [
        "http://id.who.int/icd/entity/1", "http://id.who.int/icd/release/11/mms/1", code, blockId, "English title", title, classKind, "1",
        "False", chapterNo ?? "01", "browser", isLeaf, "True", g1, g2, g3, g4, g5, string.Empty, string.Empty, string.Empty, string.Empty,
    ];

    /// <summary>Planilha em memória (sem passar por arquivo), com linhas numeradas a partir de 2 como no Excel.</summary>
    public static Planilha EmMemoria(string[] cabecalhos, params string[][] linhas) =>
        new("Aba", cabecalhos, linhas, Enumerable.Range(2, linhas.Length).ToList());

    /// <summary>Planilha em memória no layout da OMS, a partir de <see cref="LinhaOms"/> convertida em texto.</summary>
    public static Planilha EmMemoriaOms(params object?[][] linhas) =>
        EmMemoria(CabecalhoOms, linhas.Select(l => l.Select(c => c?.ToString() ?? string.Empty).ToArray()).ToArray());

    public static MemoryStream Xlsx(string[] cabecalhos, params object?[][] linhas) =>
        Xlsx(("Plan1", cabecalhos, linhas));

    /// <summary>Gera um .xlsx real. Texto como "01" é gravado como texto, números como número.</summary>
    public static MemoryStream Xlsx(params (string Aba, string[] Cabecalhos, object?[][] Linhas)[] abas)
    {
        using var livro = new XLWorkbook();
        foreach (var (nome, cabecalhos, linhas) in abas)
        {
            var aba = livro.AddWorksheet(nome);
            for (var c = 0; c < cabecalhos.Length; c++)
                aba.Cell(1, c + 1).Value = cabecalhos[c];

            for (var l = 0; l < linhas.Length; l++)
            {
                for (var c = 0; c < linhas[l].Length; c++)
                {
                    aba.Cell(l + 2, c + 1).Value = linhas[l][c] switch
                    {
                        null => Blank.Value,
                        string s => s,
                        int i => i,
                        double d => d,
                        bool b => b,
                        var outro => throw new NotSupportedException($"Tipo {outro.GetType()} não suportado no teste."),
                    };
                }
            }
        }

        var memoria = new MemoryStream();
        livro.SaveAs(memoria);
        memoria.Position = 0;
        return memoria;
    }
}
