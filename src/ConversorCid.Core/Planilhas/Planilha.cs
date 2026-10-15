namespace ConversorCid.Planilhas;

/// <summary>
/// Tabela lida de uma planilha: a primeira linha é o cabeçalho e as demais são dados.
/// </summary>
public sealed class Planilha
{
    private readonly IReadOnlyDictionary<string, int> _indices;

    public Planilha(string nomeAba, IReadOnlyList<string> cabecalhos, IReadOnlyList<string[]> celulasDasLinhas, IReadOnlyList<int> numerosDasLinhas)
    {
        NomeAba = nomeAba;
        Cabecalhos = cabecalhos;

        // Cabeçalho repetido ou vazio: vale o primeiro; o vazio nunca é endereçável.
        var indices = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < cabecalhos.Count; i++)
        {
            if (cabecalhos[i].Length > 0)
                indices.TryAdd(cabecalhos[i], i);
        }
        _indices = indices;

        var linhas = new List<LinhaPlanilha>(celulasDasLinhas.Count);
        for (var i = 0; i < celulasDasLinhas.Count; i++)
            linhas.Add(new LinhaPlanilha(numerosDasLinhas[i], _indices, celulasDasLinhas[i]));
        Linhas = linhas;
    }

    public string NomeAba { get; }

    public IReadOnlyList<string> Cabecalhos { get; }

    public IReadOnlyList<LinhaPlanilha> Linhas { get; }

    public bool ContemColuna(string nome) => _indices.ContainsKey(nome);
}

/// <summary>
/// Uma linha de dados, endereçável pelo nome da coluna do cabeçalho.
/// </summary>
public sealed class LinhaPlanilha
{
    private readonly IReadOnlyDictionary<string, int> _indices;
    private readonly string[] _celulas;

    internal LinhaPlanilha(int numero, IReadOnlyDictionary<string, int> indices, string[] celulas)
    {
        Numero = numero;
        _indices = indices;
        _celulas = celulas;
    }

    /// <summary>Número da linha na planilha (1 é o cabeçalho), para mensagens ao usuário.</summary>
    public int Numero { get; }

    /// <summary>Valor da célula; vazio quando a célula não tem conteúdo.</summary>
    /// <exception cref="ArgumentException">A coluna não existe no cabeçalho.</exception>
    public string this[string coluna]
    {
        get
        {
            if (!_indices.TryGetValue(coluna, out var indice))
                throw new ArgumentException($"A coluna '{coluna}' não existe na planilha.", nameof(coluna));
            return indice < _celulas.Length ? _celulas[indice] : string.Empty;
        }
    }
}
