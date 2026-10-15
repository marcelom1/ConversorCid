using ConversorCid.Planilhas;

namespace ConversorCid.Perfis;

/// <summary>
/// Um arquivo que o perfil lê. A maioria dos perfis tem uma entrada (a planilha); o CID-10 tem quatro.
/// </summary>
/// <param name="Nome">Chave com que o perfil recebe a tabela em <see cref="IPerfilConversao.Converter"/> (ex.: "grupos").</param>
/// <param name="Descricao">Texto para o usuário, usado nas mensagens de erro.</param>
/// <param name="ColunasObrigatorias">Colunas que o cabeçalho precisa ter, pelo nome.</param>
/// <param name="Leitor">Como o arquivo é lido (xlsx, CSV...).</param>
/// <param name="NomeDoArquivo">
/// Nome esperado do arquivo quando a entrada é uma pasta (comparação sem diferenciar caixa).
/// <c>null</c> nos perfis de arquivo único, em que o usuário informa o arquivo diretamente.
/// </param>
public sealed record EntradaDoPerfil(
    string Nome,
    string Descricao,
    IReadOnlyList<string> ColunasObrigatorias,
    IPlanilhaLeitor Leitor,
    string? NomeDoArquivo = null);
