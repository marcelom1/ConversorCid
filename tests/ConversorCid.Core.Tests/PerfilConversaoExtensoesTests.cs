using ConversorCid.Conversao;
using ConversorCid.Perfis;

namespace ConversorCid.Core.Tests;

public class PerfilConversaoExtensoesTests : IDisposable
{
    private readonly string _pasta = Path.Combine(Path.GetTempPath(), "ConversorCidTests", Guid.NewGuid().ToString("N"));

    public PerfilConversaoExtensoesTests() => Directory.CreateDirectory(_pasta);

    public void Dispose()
    {
        if (Directory.Exists(_pasta))
            Directory.Delete(_pasta, recursive: true);
    }

    [Fact]
    public void Cid11_pede_um_arquivo_e_e_uma_planilha()
    {
        var perfil = new PerfilCid11();

        Assert.True(perfil.EntradaEhArquivo());
        Assert.True(perfil.EntradaEhPlanilha());
    }

    [Fact]
    public void Cid10_pede_uma_pasta_e_nao_tem_aba_para_escolher()
    {
        var perfil = new PerfilCid10();

        Assert.False(perfil.EntradaEhArquivo());
        Assert.False(perfil.EntradaEhPlanilha());
    }

    [Fact]
    public void Todo_perfil_do_catalogo_tem_um_tipo_de_entrada_coerente_com_o_que_o_conversor_aceita()
    {
        // A interface decide arquivo ou pasta por EntradaEhArquivo; o conversor, por ResolverEntradas. Têm de concordar.
        foreach (var perfil in CatalogoPerfis.Todos)
        {
            var arquivo = Path.Combine(_pasta, "entrada.xlsx");
            File.WriteAllText(arquivo, "x");

            if (perfil.EntradaEhArquivo())
                Assert.Single(ConversorParaCsv.ResolverEntradas(arquivo, perfil));
            else
                Assert.Throws<ConversorCid.Planilhas.PlanilhaInvalidaException>(() => ConversorParaCsv.ResolverEntradas(arquivo, perfil));
        }
    }

    [Fact]
    public void Saida_padrao_de_um_arquivo_fica_ao_lado_dele_com_o_nome_do_perfil()
    {
        var entrada = Path.Combine(_pasta, "Exemplo Cid11.xlsx");

        Assert.Equal(Path.Combine(_pasta, "Cid11.csv"), ConversorParaCsv.SaidaPadrao(entrada, new PerfilCid11()));
    }

    [Fact]
    public void Saida_padrao_de_uma_pasta_fica_dentro_dela()
    {
        Assert.Equal(Path.Combine(_pasta, "Cid10.csv"), ConversorParaCsv.SaidaPadrao(_pasta, new PerfilCid10()));
    }
}
