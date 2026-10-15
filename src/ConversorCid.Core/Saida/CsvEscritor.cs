using System.Text;

namespace ConversorCid.Saida;

/// <summary>
/// Grava o CSV no formato que o Benner importa: delimitador <c>|</c>, UTF-8 com BOM, quebra de linha CRLF
/// (inclusive na última linha) e nenhum campo entre aspas. Por isso os perfis precisam entregar campos
/// sem o delimitador e sem quebras de linha; este escritor recusa o que não estiver assim em vez de
/// gravar um arquivo desalinhado.
/// </summary>
public static class CsvEscritor
{
    public const char Delimitador = '|';

    private const string QuebraDeLinha = "\r\n";

    public static void Escrever(Stream destino, IReadOnlyList<string> cabecalho, IEnumerable<IReadOnlyList<string>> linhas)
    {
        // O BOM é escrito à mão para valer também em fluxos que não começam na posição zero.
        destino.Write(Encoding.UTF8.GetPreamble());

        using var escritor = new StreamWriter(destino, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true)
        {
            NewLine = QuebraDeLinha,
        };

        EscreverLinha(escritor, cabecalho, cabecalho.Count);
        foreach (var linha in linhas)
            EscreverLinha(escritor, linha, cabecalho.Count);
    }

    private static void EscreverLinha(StreamWriter escritor, IReadOnlyList<string> campos, int colunasEsperadas)
    {
        if (campos.Count != colunasEsperadas)
            throw new InvalidOperationException($"A linha tem {campos.Count} campos e o cabeçalho tem {colunasEsperadas}.");

        for (var i = 0; i < campos.Count; i++)
        {
            var campo = campos[i];
            if (campo.AsSpan().IndexOfAny(Delimitador, '\r', '\n') >= 0)
                throw new InvalidOperationException($"O campo '{campo}' tem o delimitador ou uma quebra de linha e não pode ser gravado sem aspas.");

            if (i > 0)
                escritor.Write(Delimitador);
            escritor.Write(campo);
        }

        escritor.WriteLine();
    }
}
