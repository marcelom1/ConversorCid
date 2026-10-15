using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

namespace ConversorCid.Planilhas;

/// <summary>Uma aba da planilha. <paramref name="Oculta"/> vale para as abas ocultas e "muito ocultas" do Excel.</summary>
public sealed record AbaDaPlanilha(string Nome, bool Oculta);

/// <summary>
/// Lista as abas de um <c>.xlsx</c> sem ler os dados. Uma planilha de trabalho costuma guardar a aba que interessa
/// oculta (a da OMS, por exemplo), por isso as ocultas aparecem aqui, marcadas.
/// </summary>
public static class AbasXlsx
{
    private static readonly XNamespace NsPlanilha = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    /// <summary>As abas, na ordem do Excel. A primeira é a que a conversão lê quando nenhuma aba é indicada.</summary>
    /// <exception cref="PlanilhaInvalidaException">O conteúdo não é um .xlsx.</exception>
    public static IReadOnlyList<AbaDaPlanilha> Listar(Stream arquivo)
    {
        ExcelPlanilhaLeitor.ValidarFormato(arquivo);

        try
        {
            using var zip = new ZipArchive(arquivo, ZipArchiveMode.Read, leaveOpen: true);
            var entrada = zip.GetEntry("xl/workbook.xml")
                ?? throw new PlanilhaInvalidaException("O arquivo não é uma planilha do Excel (.xlsx): falta o livro de trabalho.");

            using var fluxo = entrada.Open();
            var livro = XDocument.Load(fluxo);

            return livro.Descendants(NsPlanilha + "sheet")
                .Select(s => new AbaDaPlanilha(
                    (string?)s.Attribute("name") ?? string.Empty,
                    ((string?)s.Attribute("state") ?? "visible") != "visible"))
                .ToList();
        }
        catch (Exception ex) when (ex is InvalidDataException or XmlException)
        {
            throw new PlanilhaInvalidaException("Não foi possível abrir o arquivo como uma planilha .xlsx. Ele pode estar corrompido.", ex);
        }
    }

    /// <summary>Como <see cref="Listar(Stream)"/>, a partir do arquivo (que pode estar aberto no Excel).</summary>
    /// <exception cref="FileNotFoundException">O arquivo não existe.</exception>
    /// <exception cref="PlanilhaInvalidaException">O conteúdo não é um .xlsx.</exception>
    public static IReadOnlyList<AbaDaPlanilha> Listar(string caminho)
    {
        using var fluxo = new FileStream(caminho, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return Listar(fluxo);
    }
}
