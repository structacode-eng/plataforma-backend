using Plataforma.Domain.Entities;

namespace Plataforma.Application.Abstractions;

public interface IUserProductAccessRepository
{
    /// <summary>Acesso do usuário a um produto, ou null se ele nunca abriu esse produto.</summary>
    Task<UserProductAccess?> GetAsync(Guid userId, string product, CancellationToken ct = default);

    /// <summary>Todos os produtos que um usuário já abriu.</summary>
    Task<IReadOnlyList<UserProductAccess>> ListByUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Acessos de vários usuários de uma vez. Existe para a listagem do painel
    /// admin não fazer uma consulta por linha (N+1) ao montar a tabela.
    /// </summary>
    Task<IReadOnlyList<UserProductAccess>> ListByUsersAsync(IEnumerable<Guid> userIds, CancellationToken ct = default);

    Task AddAsync(UserProductAccess acesso, CancellationToken ct = default);
}
