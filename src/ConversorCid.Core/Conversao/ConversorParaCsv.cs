using ConversorCid.Perfis;
using ConversorCid.Planilhas;
using ConversorCid.Saida;

namespace ConversorCid.Conversao;

/// <summary>
/// Orquestra a conversão: lê as entradas do perfil, valida as colunas exigidas, converte e grava o CSV.
/// É o ponto de entrada tanto do console quanto da futura interface.
/// </summary>
public sealed class ConversorParaCsv
{
    /// <summary>
    /// Converte uma entrada só (perfis de arquivo único, como o CID-11).
    /// </summary>
    /// <exception cref="PlanilhaInvalidaException">Formato, aba ou colunas não servem ao perfil.</exception>
    /// <exception cref="ArgumentException">O perfil lê vários arquivos; use a sobrecarga com dicionário.</exception>
    public ResultadoConversao Converter(Stream entrada, Stream csv, IPerfilConversao perfil, string? aba = null)
    {
        if (perfil.Entradas.Count != 1)
            throw new ArgumentException($"O perfil '{perfil.Nome}' lê {perfil.Entradas.Count} arquivos; informe um fluxo para cada um.", nameof(perfil));

        return Executar(new Dictionary<string, Stream> { [perfil.Entradas[0].Nome] = entrada }, csv, perfil, aba, caminhoSaida: null);
    }

    /// <summary>
    /// Converte várias entradas, indexadas por <see cref="EntradaDoPerfil.Nome"/>.
    /// </summary>
    /// <exception cref="PlanilhaInvalidaException">Formato, aba ou colunas não servem ao perfil.</exception>
    public ResultadoConversao Converter(IReadOnlyDictionary<string, Stream> entradas, Stream csv, IPerfilConversao perfil, string? aba = null) =>
        Executar(entradas, csv, perfil, aba, caminhoSaida: null);

    /// <summary>
    /// Converte arquivo (ou pasta) para arquivo. O CSV é gravado num temporário e só então assume o nome final,
    /// para que uma falha no meio não deixe um arquivo pela metade (nem apague o que já existia).
    /// </summary>
    /// <param name="caminhoEntrada">
    /// O arquivo, nos perfis de uma entrada, ou a pasta que contém os arquivos, nos perfis de várias.
    /// </param>
    /// <exception cref="PlanilhaInvalidaException">Formato, aba ou colunas não servem ao perfil.</exception>
    /// <exception cref="FileNotFoundException">Uma entrada não existe.</exception>
    /// <exception cref="IOException">A saída já existe e <paramref name="sobrescrever"/> é falso, ou o arquivo está em uso.</exception>
    public ResultadoConversao Converter(string caminhoEntrada, string caminhoCsv, IPerfilConversao perfil, string? aba = null, bool sobrescrever = false)
    {
        var arquivos = ResolverEntradas(caminhoEntrada, perfil);
        var saida = Path.GetFullPath(caminhoCsv);

        if (arquivos.Values.Any(a => string.Equals(a, saida, StringComparison.OrdinalIgnoreCase)))
            throw new IOException("O arquivo de saída não pode ser um dos arquivos de entrada.");

        if (File.Exists(saida) && !sobrescrever)
            throw new IOException($"O arquivo de saída '{saida}' já existe.");

        var pasta = Path.GetDirectoryName(saida)!;
        Directory.CreateDirectory(pasta);
        var temporario = Path.Combine(pasta, $"{Path.GetFileName(saida)}.{Guid.NewGuid():N}.tmp");

        var abertos = new List<FileStream>();
        try
        {
            ResultadoConversao resultado;

            // FileShare.ReadWrite: é comum a planilha estar aberta no Excel enquanto se converte.
            var fluxos = new Dictionary<string, Stream>();
            foreach (var (nome, caminho) in arquivos)
            {
                var fluxo = new FileStream(caminho, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                abertos.Add(fluxo);
                fluxos[nome] = fluxo;
            }

            using (var destino = new FileStream(temporario, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                resultado = Executar(fluxos, destino, perfil, aba, saida);
            }

            foreach (var fluxo in abertos)
                fluxo.Dispose();
            abertos.Clear();

            File.Move(temporario, saida, overwrite: true);
            return resultado;
        }
        finally
        {
            foreach (var fluxo in abertos)
                fluxo.Dispose();

            if (File.Exists(temporario))
                File.Delete(temporario);
        }
    }

    /// <summary>
    /// Onde o CSV é gravado quando o usuário não escolhe: ao lado da entrada (ou dentro da pasta), com o nome que o RH
    /// espera para o recurso (<see cref="IPerfilConversao.ArquivoSaidaPadrao"/>).
    /// </summary>
    public static string SaidaPadrao(string caminhoEntrada, IPerfilConversao perfil)
    {
        var pasta = Directory.Exists(caminhoEntrada) ? caminhoEntrada : Path.GetDirectoryName(Path.GetFullPath(caminhoEntrada))!;
        return Path.Combine(pasta, perfil.ArquivoSaidaPadrao);
    }

    /// <summary>
    /// Onde estão os arquivos do perfil: o próprio arquivo (perfil de uma entrada) ou, numa pasta, cada arquivo
    /// pelo nome que o perfil espera.
    /// </summary>
    internal static IReadOnlyDictionary<string, string> ResolverEntradas(string caminhoEntrada, IPerfilConversao perfil)
    {
        var caminho = Path.GetFullPath(caminhoEntrada);

        if (File.Exists(caminho))
        {
            if (perfil.Entradas.Count != 1)
                throw new PlanilhaInvalidaException($"O perfil '{perfil.Nome}' lê vários arquivos; informe a pasta que os contém, não um arquivo.");

            return new Dictionary<string, string> { [perfil.Entradas[0].Nome] = caminho };
        }

        if (!Directory.Exists(caminho))
            throw new FileNotFoundException($"'{caminho}' não foi encontrado.", caminho);

        var porNome = Directory.EnumerateFiles(caminho)
            .GroupBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key!, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var encontrados = new Dictionary<string, string>();
        var ausentes = new List<string>();
        foreach (var entrada in perfil.Entradas)
        {
            if (entrada.NomeDoArquivo is null)
                throw new PlanilhaInvalidaException($"O perfil '{perfil.Nome}' lê {entrada.Descricao}; informe o arquivo, não uma pasta.");

            if (porNome.TryGetValue(entrada.NomeDoArquivo, out var arquivo))
                encontrados[entrada.Nome] = arquivo;
            else
                ausentes.Add(entrada.NomeDoArquivo);
        }

        if (ausentes.Count > 0)
            throw new FileNotFoundException($"Na pasta '{caminho}' faltam: {string.Join(", ", ausentes)}.", caminho);

        return encontrados;
    }

    private static ResultadoConversao Executar(IReadOnlyDictionary<string, Stream> fluxos, Stream csv, IPerfilConversao perfil, string? aba, string? caminhoSaida)
    {
        var tabelas = new Dictionary<string, Planilha>();
        var resumo = new List<EntradaLida>();

        foreach (var entrada in perfil.Entradas)
        {
            if (!fluxos.TryGetValue(entrada.Nome, out var fluxo))
                throw new ArgumentException($"Falta a entrada '{entrada.Nome}' ({entrada.Descricao}).", nameof(fluxos));

            var tabela = LerValidando(entrada, fluxo, aba, perfil);
            tabelas[entrada.Nome] = tabela;
            resumo.Add(new EntradaLida(entrada.Nome, entrada.NomeDoArquivo ?? tabela.NomeAba, tabela.Linhas.Count));
        }

        var avisos = new ColetorAvisos();
        var linhas = perfil.Converter(tabelas, avisos);

        CsvEscritor.Escrever(csv, perfil.ColunasSaida, linhas);

        return new ResultadoConversao(perfil.Nome, resumo, linhas.Count, caminhoSaida, avisos.Avisos);
    }

    private static Planilha LerValidando(EntradaDoPerfil entrada, Stream fluxo, string? aba, IPerfilConversao perfil)
    {
        Planilha tabela;
        try
        {
            tabela = entrada.Leitor.Ler(fluxo, aba);
        }
        catch (PlanilhaInvalidaException ex) when (perfil.Entradas.Count > 1)
        {
            // Com vários arquivos, a mensagem precisa dizer qual deles falhou.
            throw new PlanilhaInvalidaException($"{entrada.NomeDoArquivo ?? entrada.Nome}: {ex.Message}", ex);
        }

        var ausentes = entrada.ColunasObrigatorias.Where(c => !tabela.ContemColuna(c)).ToList();
        if (ausentes.Count == 0)
            return tabela;

        var onde = perfil.Entradas.Count > 1 ? $"O arquivo {entrada.NomeDoArquivo}" : $"A aba '{tabela.NomeAba}'";
        throw new PlanilhaInvalidaException(
            $"{onde} não tem as colunas exigidas pelo perfil '{perfil.Nome}': {string.Join(", ", ausentes)}. " +
            "Confira se escolheu o perfil certo e se a primeira linha é o cabeçalho.");
    }
}
