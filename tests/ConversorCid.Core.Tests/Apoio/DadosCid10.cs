using System.Text;
using ConversorCid.Planilhas;

namespace ConversorCid.Core.Tests.Apoio;

/// <summary>Dados no layout dos CSVs do DATASUS, para os testes do perfil <c>cid10</c>.</summary>
internal static class DadosCid10
{
    public sealed record Item(string Codigo, string Descricao, string Classif = "", string Refer = "");

    /// <summary>Atalho para um item de categoria/subcategoria (evita a ambiguidade do <c>new(...)</c> em <c>params</c>).</summary>
    public static Item I(string codigo, string descricao, string classif = "", string refer = "") => new(codigo, descricao, classif, refer);

    public static Planilha Capitulos(params (string Numero, string Inicio, string Fim, string Descricao)[] linhas) =>
        Planilhas.EmMemoria(["NUMCAP", "CATINIC", "CATFIM", "DESCRICAO"],
            linhas.Select(l => new[] { l.Numero, l.Inicio, l.Fim, l.Descricao }).ToArray());

    public static Planilha Grupos(params (string Inicio, string Fim, string Descricao)[] linhas) =>
        Planilhas.EmMemoria(["CATINIC", "CATFIM", "DESCRICAO"],
            linhas.Select(l => new[] { l.Inicio, l.Fim, l.Descricao }).ToArray());

    public static Planilha Categorias(params Item[] linhas) =>
        Planilhas.EmMemoria(["CAT", "CLASSIF", "DESCRICAO", "REFER"],
            linhas.Select(l => new[] { l.Codigo, l.Classif, l.Descricao, l.Refer }).ToArray());

    public static Planilha Subcategorias(params Item[] linhas) =>
        Planilhas.EmMemoria(["SUBCAT", "CLASSIF", "DESCRICAO", "REFER"],
            linhas.Select(l => new[] { l.Codigo, l.Classif, l.Descricao, l.Refer }).ToArray());

    public static Dictionary<string, Planilha> Entradas(Planilha capitulos, Planilha grupos, Planilha categorias, Planilha subcategorias) =>
        new()
        {
            ["capitulos"] = capitulos,
            ["grupos"] = grupos,
            ["categorias"] = categorias,
            ["subcategorias"] = subcategorias,
        };

    /// <summary>
    /// Dois capítulos, um deles com grupos aninhados em 3 níveis (C00-C97 &gt; C00-C75 &gt; C00-C14) e uma categoria
    /// sem subdivisão (A09, que aparece também em SUBCATEGORIAS com o mesmo código).
    /// </summary>
    public static Dictionary<string, Planilha> Mini() => Entradas(
        Capitulos(
            ("1", "A00", "B99", "Capítulo I - Algumas doenças infecciosas e parasitárias"),
            ("2", "C00", "D48", "Capítulo II - Neoplasias [tumores]")),
        Grupos(
            ("A00", "A09", "Doenças infecciosas intestinais"),
            ("A15", "A19", "Tuberculose"),
            ("C00", "C97", "Neoplasias malignas"),
            ("C00", "C75", "Neoplasias malignas de localizações especificadas"),
            ("C00", "C14", "Neoplasias malignas do lábio, cavidade oral e faringe")),
        Categorias(
            I("A00", "Cólera"),
            I("A09", "Diarréia e gastroenterite de origem infecciosa presumível"),
            I("A15", "Tuberculose respiratória"),
            I("C00", "Neoplasia maligna do lábio")),
        Subcategorias(
            I("A000", "Cólera devida a Vibrio cholerae 01, biótipo cholerae"),
            I("A001", "Cólera devida a Vibrio cholerae 01, biótipo El Tor"),
            I("A09", "Diarréia e gastroenterite de origem infecciosa presumível"),
            I("A150", "Tuberculose pulmonar confirmada"),
            I("C000", "Neoplasia maligna do lábio superior, externo")));

    /// <summary>Arquivo de texto como o DATASUS entrega: Windows-1252, ';' e uma ';' sobrando no fim de cada linha.</summary>
    public static byte[] ArquivoDatasus(string cabecalho, params string[] linhas)
    {
        var texto = string.Join("\r\n", new[] { cabecalho }.Concat(linhas).Select(l => l + ";")) + "\r\n";
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1252).GetBytes(texto);
    }

    /// <summary>Grava os quatro arquivos numa pasta, com os nomes (e a caixa) em que o DATASUS os publica.</summary>
    public static void GravarPasta(string pasta, bool nomesEmMinusculas = false)
    {
        Directory.CreateDirectory(pasta);
        string Nome(string n) => Path.Combine(pasta, nomesEmMinusculas ? n.ToLowerInvariant() : n);

        File.WriteAllBytes(Nome("CID-10-CAPITULOS.CSV"), ArquivoDatasus("NUMCAP;CATINIC;CATFIM;DESCRICAO;DESCRABREV",
            "1;A00;B99;Capítulo I - Algumas doenças infecciosas e parasitárias;I.   Algumas doenças infecciosas e parasitárias",
            "2;C00;D48;Capítulo II - Neoplasias [tumores];II.  Neoplasias (tumores)"));
        File.WriteAllBytes(Nome("CID-10-GRUPOS.CSV"), ArquivoDatasus("CATINIC;CATFIM;DESCRICAO;DESCRABREV",
            "A00;A09;Doenças infecciosas intestinais;Doenças infecciosas intestinais",
            "C00;C97;Neoplasias [tumores] malignas(os);Neoplasias malignas"));
        File.WriteAllBytes(Nome("CID-10-CATEGORIAS.CSV"), ArquivoDatasus("CAT;CLASSIF;DESCRICAO;DESCRABREV;REFER;EXCLUIDOS",
            "A00;;Cólera;A00   Colera;;",
            "A09;;Diarréia e gastroenterite de origem infecciosa presumível;A09   Diarreia;;",
            "C00;;Neoplasia maligna do lábio;C00   Neoplasia;;"));
        File.WriteAllBytes(Nome("CID-10-SUBCATEGORIAS.CSV"), ArquivoDatasus("SUBCAT;CLASSIF;RESTRSEXO;CAUSAOBITO;DESCRICAO;DESCRABREV;REFER;EXCLUIDOS",
            "A000;;;;Cólera devida a Vibrio cholerae 01, biótipo cholerae;A00.0 Colera;;",
            "A09;;;;Diarréia e gastroenterite de origem infecciosa presumível;A09 Diarreia;;",
            "C000;;;;Neoplasia maligna do lábio superior, externo;C00.0 Neoplasia;;"));
    }
}
