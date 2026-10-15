namespace ConversorCid.Planilhas;

public interface IPlanilhaLeitor
{
    /// <summary>Lê uma aba da planilha.</summary>
    /// <param name="arquivo">Conteúdo do arquivo.</param>
    /// <param name="aba">Nome da aba; <c>null</c> lê a primeira.</param>
    /// <exception cref="PlanilhaInvalidaException">O conteúdo não é uma planilha ou a aba não existe.</exception>
    Planilha Ler(Stream arquivo, string? aba = null);
}
