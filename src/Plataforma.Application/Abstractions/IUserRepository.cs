using Plataforma.Domain.Entities;

namespace Plataforma.Application.Abstractions;

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default);
    Task<IReadOnlyList<User>> ListAsync(int limit = 500, CancellationToken ct = default);
    Task AddAsync(User user, CancellationToken ct = default);
}
