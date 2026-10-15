namespace ConversorCid.Perfis;

/// <summary>Um trecho da dica que é um link: o <paramref name="Texto"/> aparece escrito na dica e leva à <paramref name="Url"/>.</summary>
public sealed record LinkDaDica(string Texto, string Url);

/// <summary>
/// De onde baixar os arquivos de entrada de um perfil, para a interface mostrar ao usuário.
/// </summary>
/// <param name="Url">A página do site oficial onde os arquivos são publicados.</param>
/// <param name="Dica">Como chegar ao arquivo a partir da página.</param>
/// <param name="LinksDaDica">Trechos da dica que também são links (cada <see cref="LinkDaDica.Texto"/> precisa estar escrito na dica).</param>
public sealed record OndeBaixar(string Url, string Dica, IReadOnlyList<LinkDaDica> LinksDaDica);
