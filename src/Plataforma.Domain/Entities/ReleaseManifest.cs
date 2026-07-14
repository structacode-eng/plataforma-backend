namespace Plataforma.Domain.Entities;

/// <summary>
/// Manifesto da versão publicada do plugin (Marco 5 — updater). O endpoint público
/// <c>GET /version</c> devolve estes campos; o cliente compara com a versão instalada
/// e, se for mais nova, avisa (ou auto-instala). Guardamos uma linha por publicação
/// (histórico); a mais recente (por <see cref="UpdatedAtUtc"/>) é a corrente.
/// </summary>
public sealed class ReleaseManifest
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Version { get; private set; } = "0.0.0";
    public string? Url { get; private set; }
    public string? Notes { get; private set; }
    public string? Sha256 { get; private set; }
    public bool Mandatory { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; } = DateTime.UtcNow;

    private ReleaseManifest() { } // EF

    public ReleaseManifest(string version, string? url, string? notes, string? sha256, bool mandatory)
    {
        Version = string.IsNullOrWhiteSpace(version) ? "0.0.0" : version.Trim();
        Url = string.IsNullOrWhiteSpace(url) ? null : url.Trim();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        Sha256 = string.IsNullOrWhiteSpace(sha256) ? null : sha256.Trim().ToLowerInvariant();
        Mandatory = mandatory;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
