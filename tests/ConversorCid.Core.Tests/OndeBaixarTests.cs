using ConversorCid.Perfis;

namespace ConversorCid.Core.Tests;

public class OndeBaixarTests
{
    [Fact]
    public void Cid10_aponta_para_o_site_do_DATASUS_e_para_a_pagina_das_tabelas()
    {
        var onde = new PerfilCid10().OndeBaixar;

        Assert.NotNull(onde);
        Assert.Equal("http://www2.datasus.gov.br/cid10/V2008/cid10.htm", onde.Url);
        var link = Assert.Single(onde.LinksDaDica);
        Assert.Equal("Tabelas da CID-10", link.Texto);
        Assert.Equal("http://www2.datasus.gov.br/cid10/V2008/download.htm", link.Url);
        Assert.Contains("arquivos em formato CSV", onde.Dica);
    }

    [Fact]
    public void Cid11_aponta_para_o_navegador_da_OMS_e_diz_onde_fica_a_planilha()
    {
        var onde = new PerfilCid11().OndeBaixar;

        Assert.NotNull(onde);
        Assert.Equal("https://icd.who.int/browse/2026-01/mms/pt", onde.Url);
        Assert.Empty(onde.LinksDaDica);
        Assert.Contains("Info", onde.Dica);
        Assert.Contains("Arquivo em planilha", onde.Dica);
    }

    [Fact]
    public void Todo_perfil_do_catalogo_traz_links_validos_e_a_dica_contem_o_texto_de_cada_link()
    {
        // A interface procura o texto do link dentro da dica para marcá-lo; texto ausente seria um link que não aparece.
        foreach (var perfil in CatalogoPerfis.Todos)
        {
            var onde = perfil.OndeBaixar;
            Assert.NotNull(onde);
            Assert.True(Uri.TryCreate(onde.Url, UriKind.Absolute, out var url) && url.Scheme is "http" or "https", $"{perfil.Nome}: {onde.Url}");

            foreach (var link in onde.LinksDaDica)
            {
                Assert.Contains(link.Texto, onde.Dica);
                Assert.True(Uri.TryCreate(link.Url, UriKind.Absolute, out var destino) && destino.Scheme is "http" or "https", $"{perfil.Nome}: {link.Url}");
            }
        }
    }
}
