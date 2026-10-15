using ConversorCid.Planilhas;

namespace ConversorCid.Perfis;

/// <summary>
/// O que a interface precisa saber de um perfil para pedir a entrada certa ao usuário (arquivo ou pasta; aba ou não).
/// Segue a mesma regra de <c>ConversorParaCsv.ResolverEntradas</c>.
/// </summary>
public static class PerfilConversaoExtensoes
{
    /// <summary>Verdadeiro quando o usuário informa um arquivo; falso quando informa a pasta que contém os arquivos do perfil.</summary>
    public static bool EntradaEhArquivo(this IPerfilConversao perfil) =>
        perfil.Entradas.Count == 1 && perfil.Entradas[0].NomeDoArquivo is null;

    /// <summary>Verdadeiro quando a entrada é uma planilha <c>.xlsx</c>, em que faz sentido escolher a aba.</summary>
    public static bool EntradaEhPlanilha(this IPerfilConversao perfil) =>
        perfil.EntradaEhArquivo() && perfil.Entradas[0].Leitor is ExcelPlanilhaLeitor;
}
