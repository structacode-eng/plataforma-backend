using Plataforma.Domain.Entities;

namespace Plataforma.Application.Abstractions;

/// <summary>
/// Emissão de access token (JWT curto) e geração/hashing de refresh token.
/// O token bruto vai para o cliente; no banco guardamos só o hash.
/// </summary>
public interface IJwtTokenService
{
    (string accessToken, DateTime expiresAtUtc) CreateAccessToken(User user);

    /// <summary>Token de sessão para o cliente desktop (mais longo que o access token curto).
    /// Usado pelo gate do plugin, que revalida periodicamente via /auth/me.</summary>
    (string token, DateTime expiresAtUtc) CreateDesktopToken(User user);

    string GenerateRefreshToken();
    string HashRefreshToken(string rawToken);
}
