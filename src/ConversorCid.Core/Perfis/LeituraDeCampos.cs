using ConversorCid.Conversao;
using ConversorCid.Planilhas;
using ConversorCid.Saida;

namespace ConversorCid.Perfis;

internal static class LeituraDeCampos
{
    /// <summary>
    /// Lê um campo já seguro para o CSV de saída. Se o texto tinha o delimitador, o conteúdo foi alterado de fato
    /// e isso vira um aviso com a linha da planilha e o item que o usuário reconhece.
    /// </summary>
    public static string Limpo(LinhaPlanilha linha, string coluna, string identificacao, ColetorAvisos avisos)
    {
        var limpo = Transformacoes.LimparCampo(linha[coluna], CsvEscritor.Delimitador, out var substituiuDelimitador);
        if (substituiuDelimitador)
            avisos.Adicionar(linha.Numero, $"{identificacao}: {coluna} continha o separador '{CsvEscritor.Delimitador}', trocado por espaço.");
        return limpo;
    }
}
