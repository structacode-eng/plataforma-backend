using Plataforma.Domain.Entities;

namespace Plataforma.Application.Abstractions;

public interface ILicenseRepository
{
    Task AddAsync(License license, CancellationToken ct = default);
    Task<License?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<License>> ListByUserAsync(Guid userId, CancellationToken ct = default);

    Task AddLicensePluginAsync(LicensePlugin licensePlugin, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetPluginSlugsAsync(Guid licenseId, CancellationToken ct = default);
}
