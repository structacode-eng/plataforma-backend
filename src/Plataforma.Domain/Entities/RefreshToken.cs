namespace Plataforma.Domain.Entities;

/// <summary>
/// Refresh token persistido (RF-AUTH-004). Guardamos apenas o HASH SHA-256 do token,
/// nunca o token em si — se o banco vazar, os tokens não são utilizáveis.
/// A rotação é obrigatória: ao renovar, o token atual é revogado e apontamos
/// para o hash que o substituiu (rastro para detectar reuso de token roubado).
/// </summary>
public sealed class RefreshToken
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public DateTime? RevokedAtUtc { get; private set; }
    public string? ReplacedByTokenHash { get; private set; }

    /// <summary>Ativo = não revogado e ainda dentro da validade.</summary>
    public bool IsActive => RevokedAtUtc is null && DateTime.UtcNow < ExpiresAtUtc;

    private RefreshToken() { } // EF Core

    public RefreshToken(Guid userId, string tokenHash, DateTime expiresAtUtc)
    {
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAtUtc = expiresAtUtc;
    }

    public void Revoke(string? replacedByTokenHash)
    {
        if (RevokedAtUtc is not null) return; // idempotente
        RevokedAtUtc = DateTime.UtcNow;
        ReplacedByTokenHash = replacedByTokenHash;
    }
}
