using Plataforma.Domain.Enums;

namespace Plataforma.Domain.Entities;

/// <summary>
/// Conta de usuário (RF-AUTH-001). A senha nunca é guardada em texto puro —
/// apenas o hash Argon2id (RNF-SEC-002). E-mail é único e normalizado em minúsculas.
/// </summary>
public sealed class User
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public bool EmailVerified { get; private set; }
    public bool IsActive { get; private set; } = true;
    public UserRole Role { get; private set; } = UserRole.Customer;
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; private set; }

    private User() { } // EF Core

    public User(string email, string passwordHash, UserRole role = UserRole.Customer)
    {
        Email = Normalize(email);
        PasswordHash = passwordHash;
        Role = role;
    }

    public void SetPasswordHash(string passwordHash)
    {
        PasswordHash = passwordHash;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkEmailVerified()
    {
        EmailVerified = true;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void SetRole(UserRole role)
    {
        Role = role;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Revoga o acesso do usuário (RF-LIC-004). Bloqueia login e invalida o /auth/me.</summary>
    public void Deactivate()
    {
        IsActive = false;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public static string Normalize(string email) => email.Trim().ToLowerInvariant();
}
