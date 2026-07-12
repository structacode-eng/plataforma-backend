using Microsoft.EntityFrameworkCore;
using Plataforma.Application.Abstractions;
using Plataforma.Domain.Entities;

namespace Plataforma.Infrastructure.Persistence.Repositories;

public sealed class PlanRepository : IPlanRepository
{
    private readonly AppDbContext _db;
    public PlanRepository(AppDbContext db) => _db = db;

    public Task<Plan?> GetBySlugAsync(string slug, CancellationToken ct = default)
        => _db.Plans.FirstOrDefaultAsync(p => p.Slug == slug, ct);

    public Task<bool> ExistsBySlugAsync(string slug, CancellationToken ct = default)
        => _db.Plans.AnyAsync(p => p.Slug == slug, ct);

    public async Task AddAsync(Plan plan, CancellationToken ct = default)
        => await _db.Plans.AddAsync(plan, ct);

    public async Task AddPlanPluginAsync(PlanPlugin planPlugin, CancellationToken ct = default)
        => await _db.PlanPlugins.AddAsync(planPlugin, ct);

    public async Task<IReadOnlyList<string>> GetPluginSlugsAsync(Guid planId, CancellationToken ct = default)
        => await (from pp in _db.PlanPlugins
                  join pl in _db.Plugins on pp.PluginId equals pl.Id
                  where pp.PlanId == planId
                  orderby pl.Slug
                  select pl.Slug).ToListAsync(ct);
}
