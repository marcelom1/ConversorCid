namespace ConversorCid.Conversao;

/// <summary>Uma entrada lida: o nome lógico no perfil, de onde veio (aba ou arquivo) e quantas linhas de dados tinha.</summary>
public sealed record EntradaLida(string Nome, string Origem, int Linhas);

public sealed record ResultadoConversao(
    string Perfil,
    IReadOnlyList<EntradaLida> Entradas,
    int LinhasGravadas,
    string? CaminhoSaida,
    IReadOnlyList<AvisoConversao> Avisos)
{
    /// <summary>Total de linhas de dados lidas em todas as entradas.</summary>
    public int LinhasLidas => Entradas.Sum(e => e.Linhas);
}
