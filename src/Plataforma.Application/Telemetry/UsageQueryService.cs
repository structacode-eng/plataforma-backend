using Plataforma.Application.Abstractions;

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

/// <summary>Monta o relatório de uso do painel admin.</summary>
public sealed class UsageQueryService
{
    public const int MaxDias = 365;
    public const int DiasPadrao = 30;

    private readonly IUsageRepository _uso;
    private readonly IUserRepository _users;

    public UsageQueryService(IUsageRepository uso, IUserRepository users)
    {
        _uso = uso;
        _users = users;
    }

    public async Task<UsoRelatorioDto> RelatorioAsync(int dias, string? produto, CancellationToken ct = default)
    {
        if (dias <= 0) dias = DiasPadrao;
        if (dias > MaxDias) dias = MaxDias;

        var ate = DateOnly.FromDateTime(DateTime.UtcNow);
        var de = ate.AddDays(-(dias - 1));   // inclusivo nas duas pontas

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
            Dias          = dias,
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
