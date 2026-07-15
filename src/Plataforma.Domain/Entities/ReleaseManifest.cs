namespace Plataforma.Domain.Entities;

/// <summary>
/// Manifesto de versão publicada do plugin (Marco 5 — updater). Cada linha pertence a
/// um <see cref="Channel"/>: <c>"stable"</c> (todas as máquinas) ou <c>"canary"</c>
/// (máquinas de teste). O pipeline publica no canário; o Owner promove canário→estável.
/// A linha mais recente (por <see cref="UpdatedAtUtc"/>) de cada canal é a corrente.
/// </summary>
public sealed class ReleaseManifest
{
    public const string Stable = "stable";
    public const string Canary = "canary";

    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Channel { get; private set; } = Stable;
    public string Version { get; private set; } = "0.0.0";
    public string? Url { get; private set; }
    public string? Notes { get; private set; }
    public string? Sha256 { get; private set; }
    public bool Mandatory { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; } = DateTime.UtcNow;

    private ReleaseManifest() { } // EF

    public ReleaseManifest(string channel, string version, string? url, string? notes, string? sha256, bool mandatory)
    {
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
}
