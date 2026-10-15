using System.Globalization;
using System.Text;
using ExcelDataReader;
using ExcelDataReader.Exceptions;

namespace ConversorCid.Planilhas;

/// <summary>
/// Lê planilhas <c>.xlsx</c> com o ExcelDataReader. Todo valor chega como texto, porque o que sai
/// dali é um CSV: números inteiros não ganham casas decimais e booleanos viram TRUE/FALSE.
/// </summary>
public sealed class ExcelPlanilhaLeitor : IPlanilhaLeitor
{
    private static readonly byte[] AssinaturaXlsx = [0x50, 0x4B];                 // "PK": um .xlsx é um zip
    private static readonly byte[] AssinaturaXls = [0xD0, 0xCF, 0x11, 0xE0];      // OLE2: .xls antigo

    static ExcelPlanilhaLeitor()
    {
        // O ExcelDataReader pede a code page 1252 ao ser criado; no .NET moderno ela precisa ser registrada.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public Planilha Ler(Stream arquivo, string? aba = null)
    {
        ValidarFormato(arquivo);

        IExcelDataReader leitor;
        try
        {
            leitor = ExcelReaderFactory.CreateOpenXmlReader(arquivo);
        }
        catch (Exception ex) when (ex is ExcelReaderException or InvalidDataException)
        {
            throw new PlanilhaInvalidaException("Não foi possível abrir o arquivo como uma planilha .xlsx. Ele pode estar corrompido.", ex);
        }

        using (leitor)
        {
            var abas = new List<string>();
            do
            {
                abas.Add(leitor.Name);
                if (aba is null || string.Equals(leitor.Name, aba, StringComparison.OrdinalIgnoreCase))
                    return LerAbaAtual(leitor);
            }
            while (leitor.NextResult());

            throw new PlanilhaInvalidaException(
                $"A aba '{aba}' não existe na planilha. Abas disponíveis: {string.Join(", ", abas.Select(a => $"'{a}'"))}.");
        }
    }

    private static Planilha LerAbaAtual(IExcelDataReader leitor)
    {
        if (!leitor.Read())
            throw new PlanilhaInvalidaException($"A aba '{leitor.Name}' está vazia.");

        var cabecalhos = new string[leitor.FieldCount];
        for (var i = 0; i < cabecalhos.Length; i++)
            cabecalhos[i] = Texto(leitor.GetValue(i)).Trim();

        var celulas = new List<string[]>();
        var numeros = new List<int>();
        var numeroDaLinha = 1;

        while (leitor.Read())
        {
            numeroDaLinha++;
            var linha = new string[cabecalhos.Length];
            var temConteudo = false;
            for (var i = 0; i < linha.Length; i++)
            {
                linha[i] = Texto(leitor.GetValue(i));
                temConteudo |= linha[i].Trim().Length > 0;
            }

            if (!temConteudo)
                continue; // a planilha costuma arrastar linhas em branco no fim

            celulas.Add(linha);
            numeros.Add(numeroDaLinha);
        }

        return new Planilha(leitor.Name, cabecalhos, celulas, numeros);
    }

    internal static void ValidarFormato(Stream arquivo)
    {
        if (!arquivo.CanSeek)
            return;

        var inicio = arquivo.Position;
        var cabecalho = new byte[4];
        var lidos = arquivo.Read(cabecalho, 0, cabecalho.Length);
        arquivo.Position = inicio;

        if (lidos >= AssinaturaXls.Length && cabecalho.AsSpan(0, AssinaturaXls.Length).SequenceEqual(AssinaturaXls))
            throw new PlanilhaInvalidaException("O arquivo está no formato antigo do Excel (.xls). Abra-o no Excel e salve como .xlsx.");

        if (lidos < AssinaturaXlsx.Length || !cabecalho.AsSpan(0, AssinaturaXlsx.Length).SequenceEqual(AssinaturaXlsx))
            throw new PlanilhaInvalidaException("O arquivo não é uma planilha do Excel (.xlsx).");
    }

    private static string Texto(object? valor) => valor switch
    {
        null => string.Empty,
        string s => s,
        double d => d == Math.Floor(d) && Math.Abs(d) < 1e15
            ? ((long)d).ToString(CultureInfo.InvariantCulture)
            : d.ToString("R", CultureInfo.InvariantCulture),
        bool b => b ? "TRUE" : "FALSE",
        DateTime data => data.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => valor.ToString() ?? string.Empty,
    };
}
