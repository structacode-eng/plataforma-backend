namespace Plataforma.Application.Auth;

public record RegisterRequest(string Email, string Password);
public record LoginRequest(string Email, string Password);
public record RefreshRequest(string RefreshToken);

/// <summary>
/// Par de tokens devolvido ao cliente. O access token é curto (15 min) e vai no header
/// Authorization; o refresh token é longo (30 dias) e o cliente guarda cifrado (DPAPI, no Marco 3).
/// </summary>
public record TokenResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc);
