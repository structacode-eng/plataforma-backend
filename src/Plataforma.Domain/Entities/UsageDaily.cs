namespace Plataforma.Domain.Entities;

/// <summary>
/// Contador de uso de uma ferramenta, agregado por dia. Uma linha por
/// (usuário, produto, comando, dia).
///
/// <para><b>Agregado e não evento cru, de propósito.</b> Guardar um registro por
/// clique responderia mais perguntas, mas cresce sem limite — e o banco (Neon)
/// tem teto de armazenamento no plano atual. Somando por dia, o volume fica na
/// casa de dezenas de milhares de linhas por mês no pior caso, e a pergunta que
/// justifica a funcionalidade ("quais ferramentas são realmente usadas, e por
/// quem") continua respondida.</para>
///
/// <para>O que se perde: linha do tempo. Não dá para dizer "fulano abriu o
/// Audita BIM às 14h32" — só que abriu 3 vezes naquele dia. Se isso fizer falta,
/// o caminho é uma tabela de eventos separada, com política de retenção.</para>
/// </summary>
public sealed class UsageDaily
{
    /// <summary>Teto de incremento por evento. O cliente manda quantidades
    /// acumuladas; sem teto, um cliente adulterado poderia mandar int.MaxValue e
    /// estourar o contador.</summary>
    public const int MaxIncremento = 5000;

    /// <summary>Tamanho máximo do identificador de comando.</summary>
    public const int MaxComando = 60;

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }

    /// <summary>Slug do produto, normalizado por <see cref="UserProductAccess.Normalize"/>.</summary>
    public string Product { get; private set; } = null!;

    /// <summary>Identificador da ferramenta (ex.: <c>audita_bim</c>).</summary>
    public string Command { get; private set; } = null!;

    /// <summary>Dia do uso, em UTC. Só a data — a hora não é guardada.</summary>
    public DateOnly Day { get; private set; }

    /// <summary>Quantas vezes a ferramenta foi aberta neste dia.</summary>
    public int Count { get; private set; }

    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; private set; } = DateTime.UtcNow;

    private UsageDaily() { } // EF

    /// <param name="command">Já normalizado por <see cref="NormalizeComando"/>.
    /// Quem monta o lote descarta os inválidos antes de chegar aqui, então um
    /// comando vazio neste ponto é erro de programação, não entrada suja.</param>
    public UsageDaily(Guid userId, string product, string command, DateOnly day)
    {
        var slug = NormalizeComando(command)
            ?? throw new ArgumentException("Comando vazio ou sem caracteres validos.", nameof(command));

        UserId = userId;
        Product = UserProductAccess.Normalize(product);
        Command = slug;
        Day = day;
    }

    /// <summary>
    /// Normaliza o identificador do comando: minúsculas e só <c>[a-z0-9_]</c>,
    /// truncado em <see cref="MaxComando"/>.
    ///
    /// <para>Aqui <b>não</b> há lista fechada, ao contrário dos produtos. A lista
    /// de ferramentas muda a cada versão do plugin, e uma whitelist no servidor
    /// ficaria velha — descartando em silêncio justamente a ferramenta nova que
    /// se quer medir. O que protege contra lixo é o conjunto de caracteres, o
    /// limite de tamanho, o teto de itens por lote e o rate limit.</para>
    ///
    /// <para>Devolve <c>null</c> se não sobrar nada utilizável; quem chama
    /// descarta o item em vez de rejeitar o lote inteiro.</para>
    /// </summary>
    public static string? NormalizeComando(string? command)
    {
        if (string.IsNullOrWhiteSpace(command)) return null;

        var origem = command.Trim().ToLowerInvariant();
        var sb = new System.Text.StringBuilder(Math.Min(origem.Length, MaxComando));
        foreach (var c in origem)
        {
            if (sb.Length >= MaxComando) break;
            if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_') sb.Append(c);
            else if (c == '-' || c == '.' || c == ' ') sb.Append('_');
        }

        var slug = sb.ToString().Trim('_');
        return slug.Length == 0 ? null : slug;
    }

    /// <summary>Soma <paramref name="quantidade"/> ao contador do dia.
    /// Valores fora de <c>[1, <see cref="MaxIncremento"/>]</c> são aparados.</summary>
    public void Increment(int quantidade)
    {
        if (quantidade < 1) return;
        if (quantidade > MaxIncremento) quantidade = MaxIncremento;

        Count += quantidade;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
