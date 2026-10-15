namespace ConversorCid.Cli;

/// <summary>Argumentos da linha de comando. Mantido à mão: são poucos e evita dependência.</summary>
internal sealed class Argumentos
{
    public bool ExibirAjuda { get; private init; }

    public bool ListarPerfis { get; private init; }

    public bool Sobrescrever { get; private init; }

    public bool TodosOsAvisos { get; private init; }

    public string? Aba { get; private init; }

    public string? Perfil { get; private init; }

    public string? Entrada { get; private init; }

    public string? Saida { get; private init; }

    /// <exception cref="ArgumentosInvalidosException">Argumento desconhecido, opção sem valor ou posicionais demais.</exception>
    public static Argumentos Interpretar(IReadOnlyList<string> args)
    {
        bool ajuda = false, listar = false, sobrescrever = false, todosAvisos = false;
        string? aba = null;
        var posicionais = new List<string>();

        for (var i = 0; i < args.Count; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "-h" or "--ajuda" or "--help" or "/?":
                    ajuda = true;
                    break;
                case "--perfis":
                    listar = true;
                    break;
                case "--sobrescrever":
                    sobrescrever = true;
                    break;
                case "--todos-avisos":
                    todosAvisos = true;
                    break;
                case "--aba":
                    if (++i >= args.Count)
                        throw new ArgumentosInvalidosException("A opção --aba precisa do nome da aba.");
                    aba = args[i];
                    break;
                default:
                    if (args[i].StartsWith('-'))
                        throw new ArgumentosInvalidosException($"Opção desconhecida: {args[i]}");
                    posicionais.Add(args[i]);
                    break;
            }
        }

        if (posicionais.Count > 3)
            throw new ArgumentosInvalidosException($"Argumentos demais: {string.Join(" ", posicionais.Skip(3))}");

        return new Argumentos
        {
            ExibirAjuda = ajuda,
            ListarPerfis = listar,
            Sobrescrever = sobrescrever,
            TodosOsAvisos = todosAvisos,
            Aba = aba,
            Perfil = posicionais.ElementAtOrDefault(0),
            Entrada = posicionais.ElementAtOrDefault(1),
            Saida = posicionais.ElementAtOrDefault(2),
        };
    }
}

internal sealed class ArgumentosInvalidosException(string message) : Exception(message);
