using Plataforma.Domain.Entities;

namespace Plataforma.Application.Abstractions;

public interface IPluginRepository
{
    Task<Plugin?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<bool> ExistsBySlugAsync(string slug, CancellationToken ct = default);
    Task<IReadOnlyList<Plugin>> ListActiveAsync(CancellationToken ct = default);
    Task AddAsync(Plugin plugin, CancellationToken ct = default);
}
