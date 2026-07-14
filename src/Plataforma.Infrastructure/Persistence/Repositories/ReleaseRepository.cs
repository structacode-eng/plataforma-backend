using Microsoft.EntityFrameworkCore;
using Plataforma.Application.Abstractions;
using Plataforma.Domain.Entities;

namespace Plataforma.Infrastructure.Persistence.Repositories;

public sealed class ReleaseRepository : IReleaseRepository
{
    private readonly AppDbContext _db;
    public ReleaseRepository(AppDbContext db) => _db = db;

    // Guardamos uma linha por publicação; a mais recente é a corrente.
    public Task<ReleaseManifest?> GetCurrentAsync(CancellationToken ct = default)
        => _db.ReleaseManifests.OrderByDescending(r => r.UpdatedAtUtc).FirstOrDefaultAsync(ct);

    public async Task AddAsync(ReleaseManifest manifest, CancellationToken ct = default)
        => await _db.ReleaseManifests.AddAsync(manifest, ct);
}
