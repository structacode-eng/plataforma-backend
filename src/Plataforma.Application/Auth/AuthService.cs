using Plataforma.Application.Abstractions;
using Plataforma.Application.Common;
using Plataforma.Domain.Entities;

namespace Plataforma.Application.Auth;

/// <summary>
/// Casos de uso de identidade: cadastro, login, renovação (com rotação) e logout.
/// Regras: senha via Argon2id; access token curto + refresh longo rotacionado (RF-AUTH-003/004).
/// </summary>
public sealed class AuthService
{
    private static readonly TimeSpan RefreshLifetime = TimeSpan.FromDays(30);

    private readonly IUserRepository _users;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtTokenService _jwt;
    private readonly IUnitOfWork _uow;

    public AuthService(
        IUserRepository users,
        IRefreshTokenRepository refreshTokens,
        IPasswordHasher hasher,
        IJwtTokenService jwt,
        IUnitOfWork uow)
    {
        _users = users;
        _refreshTokens = refreshTokens;
        _hasher = hasher;
        _jwt = jwt;
        _uow = uow;
    }

    public async Task<Result<TokenResponse>> RegisterAsync(RegisterRequest req, CancellationToken ct = default)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.Email) || !req.Email.Contains('@'))
            return Result<TokenResponse>.Fail("E-mail inválido.", "invalid_email");
        if (string.IsNullOrEmpty(req.Password) || req.Password.Length < 8)
            return Result<TokenResponse>.Fail("A senha deve ter ao menos 8 caracteres.", "weak_password");

        var email = User.Normalize(req.Email);
        if (await _users.ExistsByEmailAsync(email, ct))
            return Result<TokenResponse>.Fail("E-mail já cadastrado.", "email_taken");

        var user = new User(email, _hasher.Hash(req.Password));
        await _users.AddAsync(user, ct);

        var (raw, token) = BuildRefreshToken(user.Id);
        await _refreshTokens.AddAsync(token, ct);
        await _uow.SaveChangesAsync(ct);

        return Result<TokenResponse>.Ok(BuildResponse(user, raw, token));
    }

    public async Task<Result<TokenResponse>> LoginAsync(LoginRequest req, CancellationToken ct = default)
    {
        var email = User.Normalize(req.Email ?? "");
        var user = await _users.GetByEmailAsync(email, ct);

        // Verifica o hash mesmo se o usuário não existe seria ideal p/ evitar timing;
        // aqui mantemos simples e retornamos a mesma mensagem genérica.
        if (user is null || !_hasher.Verify(req.Password ?? "", user.PasswordHash))
            return Result<TokenResponse>.Fail("Credenciais inválidas.", "invalid_credentials");

        var (raw, token) = BuildRefreshToken(user.Id);
        await _refreshTokens.AddAsync(token, ct);
        await _uow.SaveChangesAsync(ct);

        return Result<TokenResponse>.Ok(BuildResponse(user, raw, token));
    }

    public async Task<Result<TokenResponse>> RefreshAsync(RefreshRequest req, CancellationToken ct = default)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.RefreshToken))
            return Result<TokenResponse>.Fail("Refresh token ausente.", "invalid_token");

        var stored = await _refreshTokens.GetByHashAsync(_jwt.HashRefreshToken(req.RefreshToken), ct);
        if (stored is null || !stored.IsActive)
            return Result<TokenResponse>.Fail("Refresh token inválido ou expirado.", "invalid_token");

        var user = await _users.GetByIdAsync(stored.UserId, ct);
        if (user is null)
            return Result<TokenResponse>.Fail("Usuário não encontrado.", "invalid_token");

        // Rotação obrigatória: revoga o atual e emite um novo par.
        var (raw, newToken) = BuildRefreshToken(user.Id);
        stored.Revoke(newToken.TokenHash);
        await _refreshTokens.AddAsync(newToken, ct);
        await _uow.SaveChangesAsync(ct);

        return Result<TokenResponse>.Ok(BuildResponse(user, raw, newToken));
    }

    public async Task<Result<bool>> LogoutAsync(RefreshRequest req, CancellationToken ct = default)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.RefreshToken))
            return Result<bool>.Fail("Refresh token ausente.", "invalid_token");

        var stored = await _refreshTokens.GetByHashAsync(_jwt.HashRefreshToken(req.RefreshToken), ct);
        if (stored is not null && stored.IsActive)
        {
            stored.Revoke(null);
            await _uow.SaveChangesAsync(ct);
        }
        return Result<bool>.Ok(true); // idempotente: logout de token já inválido é "sucesso"
    }

    private (string raw, RefreshToken token) BuildRefreshToken(Guid userId)
    {
        var raw = _jwt.GenerateRefreshToken();
        var token = new RefreshToken(userId, _jwt.HashRefreshToken(raw), DateTime.UtcNow.Add(RefreshLifetime));
        return (raw, token);
    }

    private TokenResponse BuildResponse(User user, string rawRefresh, RefreshToken token)
    {
        var (access, accessExp) = _jwt.CreateAccessToken(user);
        return new TokenResponse(access, accessExp, rawRefresh, token.ExpiresAtUtc);
    }
}
