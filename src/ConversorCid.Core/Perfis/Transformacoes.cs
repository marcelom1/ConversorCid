using System.Text.RegularExpressions;

namespace ConversorCid.Perfis;

/// <summary>Tratamentos de valor reutilizáveis pelos perfis.</summary>
internal static partial class Transformacoes
{
    /// <summary>
    /// A OMS marca a profundidade do título com "- " repetido ("- - - Cólera"). O CSV leva só o texto.
    /// </summary>
    public static string RemoverMarcadoresDeNivel(string? texto) =>
        MarcadoresDeNivel().Replace((texto ?? string.Empty).Trim(), string.Empty).Trim();

    /// <summary>
    /// O DATASUS escreve o título do capítulo do CID-10 com o numeral ("Capítulo III  - Doenças do sangue...").
    /// O CSV leva só o texto; o número vai em <c>ChapterNo</c>.
    /// </summary>
    public static string RemoverPrefixoDeCapitulo(string? texto) =>
        PrefixoDeCapitulo().Replace((texto ?? string.Empty).Trim(), string.Empty).Trim();

    /// <summary>
    /// Deixa o texto seguro para um CSV sem aspas: o delimitador e as quebras de linha viram espaço,
    /// espaços repetidos viram um só e as pontas são aparadas.
    /// </summary>
    /// <param name="substituiuDelimitador">O texto tinha o delimitador, ou seja, o conteúdo foi alterado de fato.</param>
    public static string LimparCampo(string? texto, char delimitador, out bool substituiuDelimitador)
    {
        texto ??= string.Empty;
        substituiuDelimitador = texto.Contains(delimitador);

        var limpo = texto
            .Replace(delimitador, ' ')
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Replace('\t', ' ');

        return EspacosRepetidos().Replace(limpo, " ").Trim();
    }

    /// <summary>"01" vira "1"; códigos não numéricos (capítulos "V" e "X") ficam como estão.</summary>
    public static string NormalizarNumeroDeCapitulo(string? texto)
    {
        var valor = (texto ?? string.Empty).Trim();
        if (valor.Length == 0 || !valor.All(char.IsAsciiDigit))
            return valor;

        var semZeros = valor.TrimStart('0');
        return semZeros.Length == 0 ? "0" : semZeros;
    }

    /// <summary>"True"/"False" (em qualquer caixa) viram TRUE/FALSE. Vazio permanece vazio.</summary>
    /// <returns><c>false</c> quando o valor não é um booleano reconhecido; <paramref name="resultado"/> traz o original.</returns>
    public static bool TentarNormalizarBooleano(string? texto, out string resultado)
    {
        var valor = (texto ?? string.Empty).Trim();
        switch (valor.ToUpperInvariant())
        {
            case "":
                resultado = string.Empty;
                return true;
            case "TRUE" or "VERDADEIRO":
                resultado = "TRUE";
                return true;
            case "FALSE" or "FALSO":
                resultado = "FALSE";
                return true;
            default:
                resultado = valor;
                return false;
        }
    }

    [GeneratedRegex(@"^(?:-(?:\s+|$))+")]
    private static partial Regex MarcadoresDeNivel();

    [GeneratedRegex(@"^Cap[ií]tulo\s+[IVXLC]+\s*-\s*", RegexOptions.IgnoreCase)]
    private static partial Regex PrefixoDeCapitulo();

    [GeneratedRegex(" {2,}")]
    private static partial Regex EspacosRepetidos();
}
