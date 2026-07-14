using Plataforma.Domain.Entities;

namespace Plataforma.Application.Abstractions;

public interface IReleaseRepository
{
    /// <summary>A release corrente (a mais recentemente publicada), ou null se nunca publicada.</summary>
    Task<ReleaseManifest?> GetCurrentAsync(CancellationToken ct = default);
    Task AddAsync(ReleaseManifest manifest, CancellationToken ct = default);
}
