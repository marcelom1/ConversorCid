namespace ConversorCid.Conversao;

/// <summary>
/// Algo que o usuário deve saber sobre os dados, sem que a conversão tenha falhado
/// (campo ajustado, tradução ausente, valor fora do padrão).
/// </summary>
/// <param name="Linha">Número da linha na planilha de entrada; 0 quando o aviso é do arquivo todo.</param>
public sealed record AvisoConversao(int Linha, string Mensagem)
{
    public override string ToString() => Linha > 0 ? $"Linha {Linha}: {Mensagem}" : Mensagem;
}

public sealed class ColetorAvisos
{
    private readonly List<AvisoConversao> _avisos = [];

    public IReadOnlyList<AvisoConversao> Avisos => _avisos;

    public void Adicionar(int linha, string mensagem) => _avisos.Add(new AvisoConversao(linha, mensagem));

    public void Adicionar(string mensagem) => Adicionar(0, mensagem);
}
