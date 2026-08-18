using Plataforma.Domain.Entities;

namespace Plataforma.Application.Abstractions;

public interface IReleaseRepository
{
    /// <summary>A release corrente de um canal ("stable"/"canary"), ou null se nunca publicada.</summary>
    /// <summary>Manifesto vigente de um produto num canal. O produto é
    /// obrigatório: sem ele a consulta devolveria o manifesto de outro
    /// produto e o cliente baixaria o instalador errado.</summary>
    Task<ReleaseManifest?> GetCurrentAsync(string product, string channel, CancellationToken ct = default);
    Task AddAsync(ReleaseManifest manifest, CancellationToken ct = default);
}
