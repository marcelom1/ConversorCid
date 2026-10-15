using System.Text;
using ConversorCid.Cli;
using ConversorCid.Conversao;
using ConversorCid.Perfis;
using ConversorCid.Planilhas;

const int Sucesso = 0;
const int UsoInvalido = 1;
const int PlanilhaInvalida = 2;
const int ErroDeArquivo = 3;
const int ErroInesperado = 4;

const int AvisosExibidosPorPadrao = 20;

Console.OutputEncoding = Encoding.UTF8;

try
{
    var argumentos = Argumentos.Interpretar(args);

    if (argumentos.ExibirAjuda || args.Length == 0)
    {
        ExibirAjuda();
        return Sucesso;
    }

    if (argumentos.ListarPerfis)
    {
        foreach (var p in CatalogoPerfis.Todos)
            Console.WriteLine($"{p.Nome,-6} {p.Descricao}");
        return Sucesso;
    }

    if (argumentos.Perfil is null || argumentos.Entrada is null)
        return Falha(UsoInvalido, "Informe o perfil e a planilha de entrada. Use --ajuda para ver os exemplos.");

    var perfil = CatalogoPerfis.Buscar(argumentos.Perfil);
    if (perfil is null)
    {
        var disponiveis = string.Join(", ", CatalogoPerfis.Todos.Select(p => p.Nome));
        return Falha(UsoInvalido, $"Perfil '{argumentos.Perfil}' desconhecido. Perfis disponíveis: {disponiveis}.");
    }

    var saida = argumentos.Saida ?? ConversorParaCsv.SaidaPadrao(argumentos.Entrada, perfil);

    var resultado = new ConversorParaCsv().Converter(argumentos.Entrada, saida, perfil, argumentos.Aba, argumentos.Sobrescrever);
    ExibirResultado(resultado, argumentos.TodosOsAvisos);
    return Sucesso;
}
catch (ArgumentosInvalidosException ex)
{
    return Falha(UsoInvalido, $"{ex.Message.TrimEnd('.')}. Use --ajuda para ver os exemplos.");
}
catch (PlanilhaInvalidaException ex)
{
    return Falha(PlanilhaInvalida, ex.Message);
}
catch (IOException ex)
{
    return Falha(ErroDeArquivo, ex.Message);
}
catch (UnauthorizedAccessException ex)
{
    return Falha(ErroDeArquivo, $"Sem permissão de acesso ao arquivo: {ex.Message}");
}
catch (Exception ex)
{
    return Falha(ErroInesperado, $"Erro inesperado: {ex}");
}

static int Falha(int codigo, string mensagem)
{
    Console.Error.WriteLine($"Erro: {mensagem}");
    return codigo;
}

static void ExibirAjuda()
{
    Console.WriteLine("""
        ConversorCid - converte os arquivos de uma classificação (CID) no CSV de importação.

        Uso:
          ConversorCid <perfil> <entrada> [saida.csv] [opções]
          ConversorCid --perfis

        <entrada> é a planilha .xlsx (cid11) ou a pasta com os CSVs do DATASUS (cid10).
        Sem saida.csv, o arquivo é gravado ao lado da planilha (ou dentro da pasta), como Cid11.csv ou Cid10.csv.

        Opções:
          --aba <nome>      Aba da planilha a converter (padrão: a primeira). Só vale para .xlsx.
          --sobrescrever    Substitui o CSV de saída se ele já existir.
          --todos-avisos    Lista todos os avisos (por padrão, só os primeiros).
          --perfis          Lista os perfis disponíveis.
          -h, --ajuda       Exibe esta ajuda.

        Exemplos:
          ConversorCid cid11 "C:\Downloads\Exemplo Cid11.xlsx" "C:\Downloads\Cid11.csv" --sobrescrever
          ConversorCid cid10 "C:\Downloads\CID10-DATASUS"
        """);
}

static void ExibirResultado(ResultadoConversao resultado, bool todosOsAvisos)
{
    Console.WriteLine($"Perfil ........: {resultado.Perfil}");
    foreach (var entrada in resultado.Entradas)
        Console.WriteLine($"Lido ..........: {entrada.Linhas,6} linhas  {entrada.Origem}");
    Console.WriteLine($"Linhas gravadas: {resultado.LinhasGravadas}");
    Console.WriteLine($"Arquivo .......: {resultado.CaminhoSaida}");

    if (resultado.Avisos.Count == 0)
        return;

    Console.WriteLine();
    Console.WriteLine($"Avisos ({resultado.Avisos.Count}):");
    var exibidos = todosOsAvisos ? resultado.Avisos : resultado.Avisos.Take(AvisosExibidosPorPadrao).ToList();
    foreach (var aviso in exibidos)
        Console.WriteLine($"  {aviso}");

    var restantes = resultado.Avisos.Count - exibidos.Count;
    if (restantes > 0)
        Console.WriteLine($"  ... e mais {restantes}. Use --todos-avisos para listar todos.");
}
