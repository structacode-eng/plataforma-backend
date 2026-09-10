using Plataforma.Application.Abstractions;
using Plataforma.Application.Common;

namespace Plataforma.Application.Telemetry;

public sealed class FerramentaDto
{
    public string Comando { get; init; } = "";
    public int Total { get; init; }
    public int Pessoas { get; init; }
}

public sealed class UsoPessoaDto
{
    public string Email { get; init; } = "";
    public string Comando { get; init; } = "";
    public int Total { get; init; }
    public string UltimoDia { get; init; } = "";
}

public sealed class UsoRelatorioDto
{
    public string De { get; init; } = "";
    public string Ate { get; init; } = "";
    public int Dias { get; init; }
    public string? Produto { get; init; }
    /// <summary>Soma de todas as aberturas no período.</summary>
    public int TotalGeral { get; init; }
    /// <summary>Pessoas distintas que abriram qualquer ferramenta no período.</summary>
    public int PessoasAtivas { get; init; }
    public IReadOnlyList<FerramentaDto> Ferramentas { get; init; } = Array.Empty<FerramentaDto>();
    public IReadOnlyList<UsoPessoaDto> PorPessoa { get; init; } = Array.Empty<UsoPessoaDto>();
}

/// <summary>Intervalo já validado, inclusivo nas duas pontas.</summary>
public readonly record struct PeriodoUso(DateOnly De, DateOnly Ate)
{
    public int Dias => Ate.DayNumber - De.DayNumber + 1;
}

/// <summary>Monta o relatório de uso do painel admin.</summary>
public sealed class UsageQueryService
{
    public const int MaxDias = 365;
    public const int DiasPadrao = 30;

    /// <summary>Quão longe no passado o início do período pode ir. Como o fim
    /// nunca passa de hoje, este limite também é o tamanho máximo da consulta.</summary>
    public const int MaxRetroativoMeses = 12;

    private readonly IUsageRepository _uso;
    private readonly IUserRepository _users;

    public UsageQueryService(IUsageRepository uso, IUserRepository users)
    {
        _uso = uso;
        _users = users;
    }

    /// <summary>
    /// Traduz o que veio da query string num intervalo utilizável. Com
    /// <paramref name="de"/> e <paramref name="ate"/> preenchidos vale o intervalo;
    /// senão, cai no modo antigo de "últimos N dias" terminando hoje.
    /// </summary>
    public static Result<PeriodoUso> ResolverPeriodo(DateOnly? de, DateOnly? ate, int dias)
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);

        if (de is null || ate is null)
        {
            if (dias <= 0) dias = DiasPadrao;
            if (dias > MaxDias) dias = MaxDias;
            return Result<PeriodoUso>.Ok(new PeriodoUso(hoje.AddDays(-(dias - 1)), hoje));
        }

        var inicio = de.Value;
        var fim = ate.Value;

        // Datas trocadas é engano de digitação, não erro: arruma em silêncio.
        if (inicio > fim) (inicio, fim) = (fim, inicio);

        // O mês corrente termina no futuro. Em vez de recusar, corta em hoje —
        // e o DTO devolve as datas efetivas, então a tela mostra o que foi usado.
        if (fim > hoje) fim = hoje;

        if (inicio > fim)
            return Result<PeriodoUso>.Fail("O período começa depois de hoje.", "periodo_invalido");

        if (inicio < hoje.AddMonths(-MaxRetroativoMeses))
            return Result<PeriodoUso>.Fail(
                $"O histórico vai até {MaxRetroativoMeses} meses atrás.", "periodo_invalido");

        return Result<PeriodoUso>.Ok(new PeriodoUso(inicio, fim));
    }

    public Task<UsoRelatorioDto> RelatorioAsync(int dias, string? produto, CancellationToken ct = default)
    {
        // O modo "últimos N dias" nunca falha na validação, então o Ok é seguro.
        var periodo = ResolverPeriodo(null, null, dias).Value;
        return RelatorioAsync(periodo, produto, ct);
    }

    public async Task<UsoRelatorioDto> RelatorioAsync(
        PeriodoUso periodo, string? produto, CancellationToken ct = default)
    {
        var (de, ate) = (periodo.De, periodo.Ate);

        var ranking = await _uso.RankingAsync(de, ate, produto, ct);
        var porPessoa = await _uso.PorPessoaAsync(de, ate, produto, ct);

        // Resolve os e-mails de uma vez. A lista de contas é pequena (dezenas),
        // então uma leitura inteira sai mais barata que uma consulta por linha.
        var usuarios = await _users.ListAsync(500, ct);
        var emailPorId = usuarios.ToDictionary(u => u.Id, u => u.Email);

        var detalhe = porPessoa
            .Select(p => new UsoPessoaDto
            {
                // Conta excluída depois do uso: mantém a linha em vez de sumir
                // com ela, senão os totais do ranking não fecham com o detalhe.
                Email     = emailPorId.TryGetValue(p.UserId, out var e) ? e : "(conta removida)",
                Comando   = p.Command,
                Total     = p.Total,
                UltimoDia = p.UltimoDia.ToString("yyyy-MM-dd"),
            })
            .ToList();

        return new UsoRelatorioDto
        {
            De            = de.ToString("yyyy-MM-dd"),
            Ate           = ate.ToString("yyyy-MM-dd"),
            Dias          = periodo.Dias,
            Produto       = string.IsNullOrWhiteSpace(produto) ? null : produto,
            TotalGeral    = ranking.Sum(r => r.Total),
            PessoasAtivas = porPessoa.Select(p => p.UserId).Distinct().Count(),
            Ferramentas   = ranking.Select(r => new FerramentaDto
            {
                Comando = r.Command,
                Total   = r.Total,
                Pessoas = r.Pessoas,
            }).ToList(),
            PorPessoa = detalhe,
        };
    }
}
