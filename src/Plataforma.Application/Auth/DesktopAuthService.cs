using Plataforma.Application.Abstractions;
using Plataforma.Application.Common;
using Plataforma.Domain.Entities;

namespace Plataforma.Application.Auth;

public sealed class DesktopSession
{
    public string Token { get; init; } = "";
    public DateTime ExpiresAtUtc { get; init; }
    public string Email { get; init; } = "";
    public string Name { get; init; } = "";
}

public sealed class DesktopMe
{
    public string Email { get; init; } = "";
    public string Name { get; init; } = "";
    public bool Active { get; init; }
}

/// <summary>
/// Autenticação para o cliente desktop no modelo "sessão" (RF-CLI-002), que o gate do
/// plugin já espera: login devolve um token de sessão longo; /me revalida periodicamente.
/// Revogação (RF-LIC-004): usuário inativo não loga e o /me passa a responder active=false.
/// </summary>
public sealed class DesktopAuthService
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtTokenService _jwt;

    public DesktopAuthService(IUserRepository users, IPasswordHasher hasher, IJwtTokenService jwt)
    {
        _users = users;
        _hasher = hasher;
        _jwt = jwt;
    }

    public async Task<Result<DesktopSession>> LoginAsync(string? email, string? password, CancellationToken ct = default)
    {
        var user = await _users.GetByEmailAsync(User.Normalize(email ?? ""), ct);
        if (user is null || !_hasher.Verify(password ?? "", user.PasswordHash))
            return Result<DesktopSession>.Fail("E-mail ou senha inválidos.", "invalid_credentials");
        if (!user.IsActive)
            return Result<DesktopSession>.Fail("Acesso revogado.", "user_inactive");

        var (token, expires) = _jwt.CreateDesktopToken(user);
        return Result<DesktopSession>.Ok(new DesktopSession
        {
            Token = token,
            ExpiresAtUtc = expires,
            Email = user.Email,
            Name = NameFromEmail(user.Email)
        });
    }

    public async Task<DesktopMe?> GetMeAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct);
        if (user is null) return null;
        return new DesktopMe { Email = user.Email, Name = NameFromEmail(user.Email), Active = user.IsActive };
    }

    private static string NameFromEmail(string email)
    {
        var at = email.IndexOf('@');
        return at > 0 ? email[..at] : email;
    }
}
