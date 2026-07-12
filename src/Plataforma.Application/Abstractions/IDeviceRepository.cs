using Plataforma.Domain.Entities;

namespace Plataforma.Application.Abstractions;

public interface IDeviceRepository
{
    Task<IReadOnlyList<Device>> ListActiveByLicenseAsync(Guid licenseId, CancellationToken ct = default);
    Task<int> CountActiveByLicenseAsync(Guid licenseId, CancellationToken ct = default);
    Task<Device?> GetActiveByLicenseAndFingerprintAsync(Guid licenseId, string fingerprint, CancellationToken ct = default);
    Task<Device?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(Device device, CancellationToken ct = default);
}
