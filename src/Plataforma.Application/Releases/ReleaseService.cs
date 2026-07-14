using Plataforma.Application.Abstractions;
using Plataforma.Application.Common;
using Plataforma.Domain.Entities;

namespace Plataforma.Application.Releases;

/// <summary>DTO público do manifesto (mapeia para o contrato GET /version do plugin).</summary>
public record ReleaseManifestDto(string Latest, string? Url, string? Notes, bool Mandatory, string? Sha256, DateTime? UpdatedAtUtc);

/// <summary>Requisição do Owner para publicar uma nova versão.</summary>
public record SetReleaseRequest(string Version, string? Url, string? Notes, string? Sha256, bool Mandatory);

/// <summary>
/// Publica/consulta a versão corrente do plugin (Marco 5 — updater). A "autoridade
/// no servidor": o Owner publica uma versão no painel e todos os clientes pegam via
/// GET /version, sem reinstalar.
/// </summary>
public sealed class ReleaseService
{
    private readonly IReleaseRepository _releases;
    private readonly IUnitOfWork _uow;

    public ReleaseService(IReleaseRepository releases, IUnitOfWork uow)
    {
        _releases = releases;
        _uow = uow;
    }

    /// <summary>Manifesto para GET /version. Se nada foi publicado, devolve 0.0.0
    /// (nenhum cliente é mais antigo que isso → nunca dispara update).</summary>
    public async Task<ReleaseManifestDto> GetManifestAsync(CancellationToken ct = default)
    {
        var r = await _releases.GetCurrentAsync(ct);
        return r is null
            ? new ReleaseManifestDto("0.0.0", null, null, false, null, null)
            : new ReleaseManifestDto(r.Version, r.Url, r.Notes, r.Mandatory, r.Sha256, r.UpdatedAtUtc);
    }

    /// <summary>Publica uma nova versão (Owner). Valida versão, URL e sha256.</summary>
    public async Task<Result<ReleaseManifestDto>> SetManifestAsync(SetReleaseRequest req, CancellationToken ct = default)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.Version))
            return Result<ReleaseManifestDto>.Fail("Versão é obrigatória (ex.: 1.8.0).", "invalid_version");
        if (!string.IsNullOrWhiteSpace(req.Url) &&
            !(req.Url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
              req.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
            return Result<ReleaseManifestDto>.Fail("URL deve começar com http:// ou https://.", "invalid_url");
        if (!string.IsNullOrWhiteSpace(req.Sha256) && !IsSha256(req.Sha256))
            return Result<ReleaseManifestDto>.Fail("SHA-256 inválido (64 caracteres hexadecimais).", "invalid_sha256");

        var manifest = new ReleaseManifest(req.Version, req.Url, req.Notes, req.Sha256, req.Mandatory);
        await _releases.AddAsync(manifest, ct);
        await _uow.SaveChangesAsync(ct);

        return Result<ReleaseManifestDto>.Ok(new ReleaseManifestDto(
            manifest.Version, manifest.Url, manifest.Notes, manifest.Mandatory, manifest.Sha256, manifest.UpdatedAtUtc));
    }

    private static bool IsSha256(string hex)
    {
        hex = hex.Trim();
        if (hex.Length != 64) return false;
        foreach (char c in hex)
            if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                return false;
        return true;
    }
}
