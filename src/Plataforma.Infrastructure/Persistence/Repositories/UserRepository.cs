using Microsoft.EntityFrameworkCore;
using Plataforma.Application.Abstractions;
using Plataforma.Domain.Entities;
using Plataforma.Domain.Enums;

namespace Plataforma.Infrastructure.Persistence.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly AppDbContext _db;
    public UserRepository(AppDbContext db) => _db = db;

    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
        => _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default)
        => _db.Users.AnyAsync(u => u.Email == email, ct);

    public async Task<IReadOnlyList<User>> ListAsync(int limit = 500, CancellationToken ct = default)
        => await _db.Users.OrderByDescending(u => u.CreatedAtUtc).Take(limit).ToListAsync(ct);

    public async Task AddAsync(User user, CancellationToken ct = default)
        => await _db.Users.AddAsync(user, ct);

    public Task<int> CountByRoleAsync(UserRole role, CancellationToken ct = default)
        => _db.Users.CountAsync(u => u.Role == role, ct);

    public void Remove(User user) => _db.Users.Remove(user);
}
