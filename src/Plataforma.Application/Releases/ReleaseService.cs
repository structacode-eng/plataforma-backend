using Plataforma.Application.Abstractions;
using Plataforma.Application.Common;
using Plataforma.Domain.Entities;

namespace Plataforma.Application.Releases;

/// <summary>DTO público do manifesto (mapeia para o contrato GET /version do plugin).</summary>
public record ReleaseManifestDto(string Latest, string? Url, string? Notes, bool Mandatory, string? Sha256, DateTime? UpdatedAtUtc);

/// <summary>Os dois canais juntos (para o painel).</summary>
public record ReleasesDto(ReleaseManifestDto Stable, ReleaseManifestDto Canary);

/// <summary>Requisição para publicar uma versão.</summary>
public record SetReleaseRequest(string Version, string? Url, string? Notes, string? Sha256, bool Mandatory);

/// <summary>
/// Publica/consulta a versão corrente do plugin por canal (Marco 5 — updater).
/// <c>stable</c> = todas as máquinas; <c>canary</c> = máquinas de teste. O pipeline
/// publica no canário; o Owner promove canário→estável quando validar.
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

    private static readonly ReleaseManifestDto Empty = new("0.0.0", null, null, false, null, null);

    private static ReleaseManifestDto ToDto(ReleaseManifest? r)
        => r is null ? Empty : new ReleaseManifestDto(r.Version, r.Url, r.Notes, r.Mandatory, r.Sha256, r.UpdatedAtUtc);

    /// <summary>Manifesto de um canal (GET /version). Default: estável. 0.0.0 se vazio.</summary>
    public async Task<ReleaseManifestDto> GetManifestAsync(string? channel = null, CancellationToken ct = default)
        => ToDto(await _releases.GetCurrentAsync(ReleaseManifest.NormalizeChannel(channel), ct));

    /// <summary>Os dois canais de uma vez (para o painel).</summary>
    public async Task<ReleasesDto> GetBothAsync(CancellationToken ct = default)
        => new ReleasesDto(
            ToDto(await _releases.GetCurrentAsync(ReleaseManifest.Stable, ct)),
            ToDto(await _releases.GetCurrentAsync(ReleaseManifest.Canary, ct)));

    /// <summary>Publica em um canal (default: estável). Valida versão, URL e sha256.</summary>
    public async Task<Result<ReleaseManifestDto>> SetManifestAsync(SetReleaseRequest req, string? channel = null, CancellationToken ct = default)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.Version))
            return Result<ReleaseManifestDto>.Fail("Versão é obrigatória (ex.: 1.8.0).", "invalid_version");
        if (!string.IsNullOrWhiteSpace(req.Url) &&
            !(req.Url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
              req.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
            return Result<ReleaseManifestDto>.Fail("URL deve começar com http:// ou https://.", "invalid_url");
        if (!string.IsNullOrWhiteSpace(req.Sha256) && !IsSha256(req.Sha256))
            return Result<ReleaseManifestDto>.Fail("SHA-256 inválido (64 caracteres hexadecimais).", "invalid_sha256");

        var m = new ReleaseManifest(ReleaseManifest.NormalizeChannel(channel), req.Version, req.Url, req.Notes, req.Sha256, req.Mandatory);
        await _releases.AddAsync(m, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<ReleaseManifestDto>.Ok(ToDto(m));
    }

    /// <summary>Promove a versão corrente do canário para o estável (libera para TODAS as máquinas).</summary>
    public async Task<Result<ReleaseManifestDto>> PromoteAsync(CancellationToken ct = default)
    {
        var c = await _releases.GetCurrentAsync(ReleaseManifest.Canary, ct);
        if (c is null || c.Version == "0.0.0")
            return Result<ReleaseManifestDto>.Fail("Nada publicado no canário para promover.", "empty_canary");
        var m = new ReleaseManifest(ReleaseManifest.Stable, c.Version, c.Url, c.Notes, c.Sha256, c.Mandatory);
        await _releases.AddAsync(m, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<ReleaseManifestDto>.Ok(ToDto(m));
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
