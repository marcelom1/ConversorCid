using System.Text;

namespace ConversorCid.Planilhas;

/// <summary>
/// Lê arquivos de texto delimitados, como os CSVs do DATASUS (<c>;</c>, Windows-1252, linha terminada em <c>;</c>).
/// <list type="bullet">
/// <item>Não há tratamento de aspas: nesses arquivos elas são caracteres do texto (<c>o "stress" e</c>), nunca delimitadores de campo.</item>
/// <item>A codificação é detectada: UTF-8 (com ou sem BOM) quando o conteúdo é UTF-8 válido, senão Windows-1252.</item>
/// <item>Uma linha com mais campos que o cabeçalho é recusada, porque indica um delimitador dentro do texto
/// e os valores sairiam deslocados; linha com menos campos é completada com vazio.</item>
/// </list>
/// </summary>
public sealed class CsvPlanilhaLeitor(char delimitador = ';') : IPlanilhaLeitor
{
    private static readonly UTF8Encoding Utf8Estrito = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    static CsvPlanilhaLeitor()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    /// <param name="aba">Não se aplica a arquivo de texto; é ignorado.</param>
    public Planilha Ler(Stream arquivo, string? aba = null)
    {
        var texto = Decodificar(LerTudo(arquivo));
        var linhas = texto.Split(["\r\n", "\n", "\r"], StringSplitOptions.None);

        // A primeira linha não vazia é o cabeçalho.
        var inicio = Array.FindIndex(linhas, l => l.Trim().Length > 0);
        if (inicio < 0)
            throw new PlanilhaInvalidaException("O arquivo está vazio.");

        var cabecalhos = linhas[inicio].Split(delimitador).Select(c => c.Trim()).ToArray();

        var celulas = new List<string[]>();
        var numeros = new List<int>();
        for (var i = inicio + 1; i < linhas.Length; i++)
        {
            if (linhas[i].Trim().Length == 0)
                continue;

            var campos = linhas[i].Split(delimitador);
            if (campos.Length > cabecalhos.Length)
            {
                throw new PlanilhaInvalidaException(
                    $"A linha {i + 1} tem {campos.Length} campos e o cabeçalho tem {cabecalhos.Length}. " +
                    $"Provavelmente há um '{delimitador}' dentro de um texto.");
            }

            var linha = new string[cabecalhos.Length];
            Array.Fill(linha, string.Empty);
            Array.Copy(campos, linha, campos.Length);

            celulas.Add(linha);
            numeros.Add(i + 1);
        }

        return new Planilha(string.Empty, cabecalhos, celulas, numeros);
    }

    private static byte[] LerTudo(Stream arquivo)
    {
        using var memoria = new MemoryStream();
        arquivo.CopyTo(memoria);
        return memoria.ToArray();
    }

    private static string Decodificar(byte[] bytes)
    {
        var bom = Encoding.UTF8.GetPreamble();
        var inicio = bytes.AsSpan().StartsWith(bom) ? bom.Length : 0;

        try
        {
            return Utf8Estrito.GetString(bytes, inicio, bytes.Length - inicio);
        }
        catch (DecoderFallbackException)
        {
            return Encoding.GetEncoding(1252).GetString(bytes, inicio, bytes.Length - inicio);
        }
    }
}
