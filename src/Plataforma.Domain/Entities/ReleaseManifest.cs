namespace Plataforma.Domain.Entities;

/// <summary>
/// Manifesto de versão publicada do plugin (Marco 5 — updater). Cada linha pertence a
/// um <see cref="Channel"/>: <c>"stable"</c> (todas as máquinas) ou <c>"canary"</c>
/// (máquinas de teste). O pipeline publica no canário; o Owner promove canário→estável.
/// A linha mais recente (por <see cref="UpdatedAtUtc"/>) de cada canal é a corrente.
/// </summary>
public sealed class ReleaseManifest
{
    /// <summary>Produtos que publicam versão. O slug do plugin é o default
    /// histórico: as linhas que existiam antes desta coluna são todas dele.</summary>
    public const string ProdutoPlugin = "revit-plugin";
    public const string ProdutoSolutions = "solutions";

    public const string Stable = "stable";
    public const string Canary = "canary";

    public Guid Id { get; private set; } = Guid.NewGuid();
    /// <summary>Produto a que este manifesto pertence. Sem esta dimensão o
    /// manifesto seria único por canal, e publicar o Solutions faria o plugin
    /// do Revit baixar o instalador errado - com o SHA-256 conferindo, porque o
    /// arquivo não está corrompido, apenas é de outro produto.</summary>
    public string Product { get; private set; } = ProdutoPlugin;

    public string Channel { get; private set; } = Stable;
    public string Version { get; private set; } = "0.0.0";
    public string? Url { get; private set; }
    public string? Notes { get; private set; }
    public string? Sha256 { get; private set; }
    public bool Mandatory { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; } = DateTime.UtcNow;

    private ReleaseManifest() { } // EF

    public ReleaseManifest(string product, string channel, string version, string? url, string? notes, string? sha256, bool mandatory)
    {
        Product = NormalizeProduct(product);
        Channel = NormalizeChannel(channel);
        Version = string.IsNullOrWhiteSpace(version) ? "0.0.0" : version.Trim();
        Url = string.IsNullOrWhiteSpace(url) ? null : url.Trim();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        Sha256 = string.IsNullOrWhiteSpace(sha256) ? null : sha256.Trim().ToLowerInvariant();
        Mandatory = mandatory;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Só existem dois canais; qualquer coisa diferente de "canary" vira "stable".</summary>
    public static string NormalizeChannel(string? c)
        => string.Equals(c?.Trim(), Canary, StringComparison.OrdinalIgnoreCase) ? Canary : Stable;

    /// <summary>
    /// Produto desconhecido - ou ausente - vira o plugin do Revit.
    ///
    /// <para>Este default é o que mantém a frota já instalada funcionando: o
    /// UpdateService do plugin em campo não manda cabeçalho de produto, então
    /// continua recebendo exatamente o manifesto que sempre recebeu. Quem
    /// publica NÃO deve depender disto - o painel e o CI informam o produto
    /// explicitamente, senão uma publicação do Solutions cairia em cima da do
    /// plugin.</para>
    /// </summary>
    public static string NormalizeProduct(string? p)
        => string.Equals(p?.Trim(), ProdutoSolutions, StringComparison.OrdinalIgnoreCase)
            ? ProdutoSolutions
            : ProdutoPlugin;
}
