using Plataforma.Domain.Entities;

namespace Plataforma.Application.Abstractions;

/// <summary>Uma linha do ranking de ferramentas.</summary>
public sealed class UsoPorComando
{
    public string Command { get; init; } = "";
    /// <summary>Total de aberturas no período.</summary>
    public int Total { get; init; }
    /// <summary>Quantas pessoas distintas abriram — separa "muita gente usa" de
    /// "uma pessoa usa muito", que o total sozinho confunde.</summary>
    public int Pessoas { get; init; }
}

/// <summary>Uso de uma pessoa em uma ferramenta, no período.</summary>
public sealed class UsoPorPessoa
{
    public Guid UserId { get; init; }
    public string Command { get; init; } = "";
    public int Total { get; init; }
    public DateOnly UltimoDia { get; init; }
}

public interface IUsageRepository
{
    /// <summary>Linha do dia para o upsert, ou null se ainda não existe.</summary>
    Task<UsageDaily?> GetAsync(Guid userId, string product, string command, DateOnly day, CancellationToken ct = default);

    /// <summary>
    /// Carrega de uma vez todas as linhas de um lote. Existe para o endpoint de
    /// telemetria não fazer uma consulta por item — um lote traz dezenas deles.
    /// </summary>
    Task<IReadOnlyList<UsageDaily>> GetManyAsync(
        Guid userId, string product, IEnumerable<string> commands, IEnumerable<DateOnly> days, CancellationToken ct = default);

    Task AddAsync(UsageDaily linha, CancellationToken ct = default);

    /// <summary>Ranking de ferramentas no período (inclusive nas duas pontas).</summary>
    Task<IReadOnlyList<UsoPorComando>> RankingAsync(DateOnly de, DateOnly ate, string? product, CancellationToken ct = default);

    /// <summary>Detalhe por pessoa no período, para o painel abrir uma ferramenta.</summary>
    Task<IReadOnlyList<UsoPorPessoa>> PorPessoaAsync(DateOnly de, DateOnly ate, string? product, CancellationToken ct = default);
}
