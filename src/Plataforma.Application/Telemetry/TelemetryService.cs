using Plataforma.Application.Abstractions;
using Plataforma.Domain.Entities;

namespace Plataforma.Application.Telemetry;

/// <summary>Um item do lote enviado pelo plugin.</summary>
public sealed class UsoEvento
{
    public string? Comando { get; set; }
    public int Quantidade { get; set; }
    /// <summary>Dia do uso, na máquina do usuário (yyyy-MM-dd).</summary>
    public DateOnly Dia { get; set; }
}

/// <summary>Resultado do processamento de um lote.</summary>
public sealed class UsoResultado
{
    public int Aceitos { get; init; }
    public int Descartados { get; init; }
}

/// <summary>
/// Recebe os lotes de uso do plugin e soma nos contadores diários.
///
/// <para>Tolerante por desenho: item inválido (comando vazio, quantidade
/// negativa, dia absurdo) é <b>descartado em silêncio</b>, não derruba o lote.
/// O plugin envia em segundo plano e não tem como reagir a um erro — rejeitar o
/// lote inteiro por causa de um item só faria perder os outros.</para>
/// </summary>
public sealed class TelemetryService
{
    /// <summary>Teto de itens por lote. O plugin agrega antes de enviar, então
    /// um lote real tem dezenas de itens; isto é só um limite de sanidade.</summary>
    public const int MaxItensPorLote = 500;

    /// <summary>Quantos dias para trás um evento pode alegar. Cobre máquina que
    /// ficou dias sem internet com a fila acumulada, sem aceitar data arbitrária
    /// (o dia vem do relógio da máquina do usuário, que pode estar errado).</summary>
    public const int MaxDiasRetroativos = 60;

    private readonly IUsageRepository _uso;
    private readonly IUnitOfWork _uow;

    public TelemetryService(IUsageRepository uso, IUnitOfWork uow)
    {
        _uso = uso;
        _uow = uow;
    }

    public async Task<UsoResultado> RegistrarAsync(
        Guid userId, string? produto, IReadOnlyList<UsoEvento>? eventos, CancellationToken ct = default)
    {
        if (eventos is null || eventos.Count == 0) return new UsoResultado();

        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var minimo = hoje.AddDays(-MaxDiasRetroativos);
        var slug = UserProductAccess.Normalize(produto);

        // 1) Higieniza e junta o que vier repetido: o mesmo comando pode chegar
        //    duas vezes no lote (fila antiga + fila da sessão), e somar antes de
        //    ir ao banco evita duas idas para a mesma linha.
        var somado = new Dictionary<(string Comando, DateOnly Dia), int>();
        var descartados = 0;

        foreach (var ev in eventos.Take(MaxItensPorLote))
        {
            var comando = UsageDaily.NormalizeComando(ev.Comando);
            if (comando is null || ev.Quantidade < 1 || ev.Dia < minimo || ev.Dia > hoje)
            {
                descartados++;
                continue;
            }

            var chave = (comando, ev.Dia);
            somado[chave] = somado.TryGetValue(chave, out var atual)
                ? atual + ev.Quantidade
                : ev.Quantidade;
        }
        descartados += Math.Max(0, eventos.Count - MaxItensPorLote);

        if (somado.Count == 0) return new UsoResultado { Descartados = descartados };

        // 2) Carrega tudo o que já existe numa consulta só (evita N+1).
        var existentes = await _uso.GetManyAsync(
            userId, slug, somado.Keys.Select(k => k.Comando), somado.Keys.Select(k => k.Dia), ct);

        var indice = existentes.ToDictionary(u => (u.Command, u.Day));

        // 3) Incrementa o que existe, cria o que falta.
        foreach (var ((comando, dia), quantidade) in somado)
        {
            if (indice.TryGetValue((comando, dia), out var linha))
            {
                linha.Increment(quantidade);
            }
            else
            {
                var nova = new UsageDaily(userId, slug, comando, dia);
                nova.Increment(quantidade);
                await _uso.AddAsync(nova, ct);
            }
        }

        await _uow.SaveChangesAsync(ct);
        return new UsoResultado { Aceitos = somado.Count, Descartados = descartados };
    }
}
