namespace ConversorCid.Perfis;

/// <summary>
/// Perfis disponíveis. Para um tipo novo de arquivo, crie o perfil e acrescente-o aqui.
/// </summary>
public static class CatalogoPerfis
{
    public static IReadOnlyList<IPerfilConversao> Todos { get; } =
    [
        new PerfilCid10(),
        new PerfilCid11(),
    ];

    public static IPerfilConversao? Buscar(string nome) =>
        Todos.FirstOrDefault(p => p.Nome.Equals(nome.Trim(), StringComparison.OrdinalIgnoreCase));
}
