using ConversorCid.Conversao;
using ConversorCid.Planilhas;

namespace ConversorCid.Perfis;

/// <summary>
/// Define como os arquivos de um determinado tipo (CID-10, CID-11...) viram o CSV de importação:
/// quais arquivos e colunas são exigidos na entrada, quais colunas saem e como cada valor é tratado.
/// Um tipo novo é um perfil novo; o restante da aplicação não muda.
/// </summary>
public interface IPerfilConversao
{
    /// <summary>Identificador usado na linha de comando e na interface (ex.: <c>cid11</c>).</summary>
    string Nome { get; }

    string Descricao { get; }

    /// <summary>Nome do CSV gerado quando o usuário não escolhe um (o mesmo do recurso que o RH embarca).</summary>
    string ArquivoSaidaPadrao { get; }

    /// <summary>De onde baixar os arquivos de entrada, para a interface mostrar. Nulo quando o perfil não tem uma fonte para indicar.</summary>
    OndeBaixar? OndeBaixar => null;

    /// <summary>Os arquivos que o perfil lê. Mais de um implica que o usuário informa uma pasta.</summary>
    IReadOnlyList<EntradaDoPerfil> Entradas { get; }

    /// <summary>Cabeçalho do CSV, na ordem de saída.</summary>
    IReadOnlyList<string> ColunasSaida { get; }

    /// <summary>
    /// Converte todas as entradas, indexadas por <see cref="EntradaDoPerfil.Nome"/>. Cada item devolvido tem uma
    /// célula por coluna de saída, e a ordem das linhas é a do arquivo final.
    /// Recebe as tabelas inteiras porque há colunas calculadas a partir de outras linhas.
    /// </summary>
    IReadOnlyList<string[]> Converter(IReadOnlyDictionary<string, Planilha> entradas, ColetorAvisos avisos);
}
