using Plataforma.Domain.Entities;

namespace Plataforma.Application.Abstractions;

public interface IPlanRepository
{
    Task<Plan?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<bool> ExistsBySlugAsync(string slug, CancellationToken ct = default);
    Task AddAsync(Plan plan, CancellationToken ct = default);
    Task AddPlanPluginAsync(PlanPlugin planPlugin, CancellationToken ct = default);

    /// <summary>Slugs dos plugins que compõem um plano (JOIN plan_plugins → plugins).</summary>
    Task<IReadOnlyList<string>> GetPluginSlugsAsync(Guid planId, CancellationToken ct = default);
}
