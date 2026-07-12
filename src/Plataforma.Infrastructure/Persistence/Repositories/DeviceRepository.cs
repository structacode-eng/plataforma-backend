using Microsoft.EntityFrameworkCore;
using Plataforma.Application.Abstractions;
using Plataforma.Domain.Entities;

namespace Plataforma.Infrastructure.Persistence.Repositories;

public sealed class DeviceRepository : IDeviceRepository
{
    private readonly AppDbContext _db;
    public DeviceRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<Device>> ListActiveByLicenseAsync(Guid licenseId, CancellationToken ct = default)
        => await _db.Devices.Where(d => d.LicenseId == licenseId && d.IsActive)
                            .OrderBy(d => d.RegisteredAtUtc)
                            .ToListAsync(ct);

    public Task<int> CountActiveByLicenseAsync(Guid licenseId, CancellationToken ct = default)
        => _db.Devices.CountAsync(d => d.LicenseId == licenseId && d.IsActive, ct);

    public Task<Device?> GetActiveByLicenseAndFingerprintAsync(Guid licenseId, string fingerprint, CancellationToken ct = default)
        => _db.Devices.FirstOrDefaultAsync(d => d.LicenseId == licenseId && d.Fingerprint == fingerprint && d.IsActive, ct);

    public Task<Device?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Devices.FirstOrDefaultAsync(d => d.Id == id, ct);

    public async Task AddAsync(Device device, CancellationToken ct = default)
        => await _db.Devices.AddAsync(device, ct);
}
