namespace Plataforma.Domain.Entities;

/// <summary>
/// Acesso de um usuário a um PRODUTO específico (plugin do Revit, Filippon
/// Solutions, ...). Uma linha por par usuário+produto.
///
/// <para>Existe porque <see cref="User.LoginCount"/> e <see cref="User.LastSeenAtUtc"/>
/// são campos únicos por conta: quando dois produtos compartilham as mesmas
/// credenciais, os dois mexem nos mesmos campos e não há como separar depois.
/// Sem esta tabela, "quantas pessoas usam o Solutions?" não tem resposta.</para>
///
/// <para>Tabela separada em vez de colunas no <see cref="User"/>: produto novo
/// vira uma linha, não uma migration.</para>
/// </summary>
public sealed class UserProductAccess
{
    /// <summary>Produtos conhecidos. O slug recebido fora desta lista vira
    /// <see cref="Desconhecido"/> — sem isto, um cliente adulterado poderia
    /// encher a tabela mandando um slug diferente a cada requisição.</summary>
    public static readonly IReadOnlySet<string> Conhecidos = new HashSet<string>
    {
        "revit-plugin",
        "solutions",
        "admin-web",
    };

    public const string Desconhecido = "desconhecido";

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }

    /// <summary>Slug do produto, sempre um valor de <see cref="Conhecidos"/> ou
    /// <see cref="Desconhecido"/>.</summary>
    public string Product { get; private set; } = null!;

    public DateTime FirstSeenAtUtc { get; private set; } = DateTime.UtcNow;
    public DateTime LastSeenAtUtc { get; private set; } = DateTime.UtcNow;
    public int LoginCount { get; private set; }

    private UserProductAccess() { } // EF

    public UserProductAccess(Guid userId, string product)
    {
        UserId = userId;
        Product = Normalize(product);
    }

    /// <summary>Normaliza e confina o slug à lista conhecida.</summary>
    public static string Normalize(string? product)
    {
        var slug = (product ?? "").Trim().ToLowerInvariant();
        return Conhecidos.Contains(slug) ? slug : Desconhecido;
    }

    /// <summary>Login novo neste produto.</summary>
    public void RegisterLogin()
    {
        LoginCount++;
        LastSeenAtUtc = DateTime.UtcNow;
    }

    /// <summary>Sinal de vida (revalidação do /auth/me), sem contar como login.</summary>
    public void MarkSeen() => LastSeenAtUtc = DateTime.UtcNow;
}
