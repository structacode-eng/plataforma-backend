using Microsoft.EntityFrameworkCore;
using Plataforma.Application.Abstractions;
using Plataforma.Domain.Entities;

namespace Plataforma.Infrastructure.Persistence.Repositories;

public sealed class LicenseRepository : ILicenseRepository
{
    private readonly AppDbContext _db;
    public LicenseRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(License license, CancellationToken ct = default)
        => await _db.Licenses.AddAsync(license, ct);

    public Task<License?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Licenses.FirstOrDefaultAsync(l => l.Id == id, ct);

    public async Task<IReadOnlyList<License>> ListByUserAsync(Guid userId, CancellationToken ct = default)
        => await _db.Licenses.Where(l => l.UserId == userId)
                             .OrderByDescending(l => l.CreatedAtUtc)
                             .ToListAsync(ct);

    public async Task AddLicensePluginAsync(LicensePlugin licensePlugin, CancellationToken ct = default)
        => await _db.LicensePlugins.AddAsync(licensePlugin, ct);

    public async Task<IReadOnlyList<string>> GetPluginSlugsAsync(Guid licenseId, CancellationToken ct = default)
        => await _db.LicensePlugins.Where(x => x.LicenseId == licenseId)
                                   .Select(x => x.PluginSlug)
                                   .ToListAsync(ct);
}
