using Microsoft.EntityFrameworkCore;
using Plataforma.Application.Abstractions;
using Plataforma.Domain.Entities;

namespace Plataforma.Infrastructure.Persistence.Repositories;

public sealed class UsageRepository : IUsageRepository
{
    private readonly AppDbContext _db;
    public UsageRepository(AppDbContext db) => _db = db;

    public Task<UsageDaily?> GetAsync(
        Guid userId, string product, string command, DateOnly day, CancellationToken ct = default)
    {
        var slug = UserProductAccess.Normalize(product);
        return _db.UsageDailies
            .FirstOrDefaultAsync(u => u.UserId == userId && u.Product == slug
                                   && u.Command == command && u.Day == day, ct);
    }

    public async Task<IReadOnlyList<UsageDaily>> GetManyAsync(
        Guid userId, string product, IEnumerable<string> commands, IEnumerable<DateOnly> days,
        CancellationToken ct = default)
    {
        var slug = UserProductAccess.Normalize(product);
        var cmds = commands.Distinct().ToArray();
        var dias = days.Distinct().ToArray();
        if (cmds.Length == 0 || dias.Length == 0) return Array.Empty<UsageDaily>();

        // Produto cartesiano comando x dia: traz um pouco mais do que o lote
        // precisa, mas em UMA consulta. O lote é pequeno (limitado no endpoint),
        // então isso é bem mais barato que uma ida ao banco por item.
        return await _db.UsageDailies
            .Where(u => u.UserId == userId && u.Product == slug
                     && cmds.Contains(u.Command) && dias.Contains(u.Day))
            .ToListAsync(ct);
    }

    public async Task AddAsync(UsageDaily linha, CancellationToken ct = default)
        => await _db.UsageDailies.AddAsync(linha, ct);

    public async Task<IReadOnlyList<UsoPorComando>> RankingAsync(
        DateOnly de, DateOnly ate, string? product, CancellationToken ct = default)
    {
        var q = Periodo(de, ate, product);

        return await q
            .GroupBy(u => u.Command)
            .Select(g => new UsoPorComando
            {
                Command = g.Key,
                Total   = g.Sum(x => x.Count),
                Pessoas = g.Select(x => x.UserId).Distinct().Count(),
            })
            .OrderByDescending(r => r.Total)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<UsoPorPessoa>> PorPessoaAsync(
        DateOnly de, DateOnly ate, string? product, CancellationToken ct = default)
    {
        var q = Periodo(de, ate, product);

        return await q
            .GroupBy(u => new { u.UserId, u.Command })
            .Select(g => new UsoPorPessoa
            {
                UserId    = g.Key.UserId,
                Command   = g.Key.Command,
                Total     = g.Sum(x => x.Count),
                UltimoDia = g.Max(x => x.Day),
            })
            .OrderByDescending(r => r.Total)
            .ToListAsync(ct);
    }

    /// <summary>Recorte comum das duas consultas do painel. <paramref name="product"/>
    /// nulo ou vazio = todos os produtos.</summary>
    private IQueryable<UsageDaily> Periodo(DateOnly de, DateOnly ate, string? product)
    {
        var q = _db.UsageDailies.Where(u => u.Day >= de && u.Day <= ate);
        if (!string.IsNullOrWhiteSpace(product))
        {
            var slug = UserProductAccess.Normalize(product);
            q = q.Where(u => u.Product == slug);
        }
        return q;
    }
}
