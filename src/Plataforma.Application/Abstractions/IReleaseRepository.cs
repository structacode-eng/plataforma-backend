using Plataforma.Domain.Entities;

namespace Plataforma.Application.Abstractions;

public interface IReleaseRepository
{
    /// <summary>A release corrente de um canal ("stable"/"canary"), ou null se nunca publicada.</summary>
    Task<ReleaseManifest?> GetCurrentAsync(string channel, CancellationToken ct = default);
    Task AddAsync(ReleaseManifest manifest, CancellationToken ct = default);
}
