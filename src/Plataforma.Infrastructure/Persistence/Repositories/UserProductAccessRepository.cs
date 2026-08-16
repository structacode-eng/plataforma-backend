using Microsoft.EntityFrameworkCore;
using Plataforma.Application.Abstractions;
using Plataforma.Domain.Entities;

namespace Plataforma.Infrastructure.Persistence.Repositories;

public sealed class UserProductAccessRepository : IUserProductAccessRepository
{
    private readonly AppDbContext _db;
    public UserProductAccessRepository(AppDbContext db) => _db = db;

    public Task<UserProductAccess?> GetAsync(Guid userId, string product, CancellationToken ct = default)
    {
        var slug = UserProductAccess.Normalize(product);
        return _db.UserProductAccesses
            .FirstOrDefaultAsync(a => a.UserId == userId && a.Product == slug, ct);
    }

    public async Task<IReadOnlyList<UserProductAccess>> ListByUserAsync(Guid userId, CancellationToken ct = default)
        => await _db.UserProductAccesses
            .Where(a => a.UserId == userId)
            .OrderBy(a => a.Product)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<UserProductAccess>> ListByUsersAsync(
        IEnumerable<Guid> userIds, CancellationToken ct = default)
    {
        var ids = userIds.Distinct().ToArray();
        if (ids.Length == 0) return Array.Empty<UserProductAccess>();

        return await _db.UserProductAccesses
            .Where(a => ids.Contains(a.UserId))
            .ToListAsync(ct);
    }

    public async Task AddAsync(UserProductAccess acesso, CancellationToken ct = default)
        => await _db.UserProductAccesses.AddAsync(acesso, ct);
}
