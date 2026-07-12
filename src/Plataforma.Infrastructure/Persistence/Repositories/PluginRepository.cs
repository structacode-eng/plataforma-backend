using Microsoft.EntityFrameworkCore;
using Plataforma.Application.Abstractions;
using Plataforma.Domain.Entities;

namespace Plataforma.Infrastructure.Persistence.Repositories;

public sealed class PluginRepository : IPluginRepository
{
    private readonly AppDbContext _db;
    public PluginRepository(AppDbContext db) => _db = db;

    public Task<Plugin?> GetBySlugAsync(string slug, CancellationToken ct = default)
        => _db.Plugins.FirstOrDefaultAsync(p => p.Slug == slug, ct);

    public Task<bool> ExistsBySlugAsync(string slug, CancellationToken ct = default)
        => _db.Plugins.AnyAsync(p => p.Slug == slug, ct);

    public async Task<IReadOnlyList<Plugin>> ListActiveAsync(CancellationToken ct = default)
        => await _db.Plugins.Where(p => p.IsActive).OrderBy(p => p.Slug).ToListAsync(ct);

    public async Task AddAsync(Plugin plugin, CancellationToken ct = default)
        => await _db.Plugins.AddAsync(plugin, ct);
}
