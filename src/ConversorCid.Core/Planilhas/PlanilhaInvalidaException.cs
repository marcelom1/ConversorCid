namespace ConversorCid.Planilhas;

/// <summary>
/// O arquivo não é uma planilha utilizável para o perfil escolhido (formato, aba ou colunas).
/// A mensagem é escrita para o usuário final.
/// </summary>
public sealed class PlanilhaInvalidaException : Exception
{
    public PlanilhaInvalidaException(string message) : base(message)
    {
    }

    public PlanilhaInvalidaException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
