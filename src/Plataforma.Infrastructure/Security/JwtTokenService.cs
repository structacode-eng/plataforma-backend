using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Plataforma.Application.Abstractions;
using Plataforma.Domain.Entities;

namespace Plataforma.Infrastructure.Security;

/// <summary>Opções do JWT lidas de configuração. A <c>Key</c> vem de segredo (user-secrets/env), nunca do appsettings versionado.</summary>
public sealed class JwtOptions
{
    public string Issuer { get; set; } = "";
    public string Audience { get; set; } = "";
    public string Key { get; set; } = "";
    public int AccessTokenMinutes { get; set; } = 15;

    /// <summary>Validade do token de sessão do cliente desktop (o gate revalida antes disso).</summary>
    public int DesktopSessionDays { get; set; } = 7;
}

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _opt;
    public JwtTokenService(IOptions<JwtOptions> opt) => _opt = opt.Value;

    public (string accessToken, DateTime expiresAtUtc) CreateAccessToken(User user)
        => BuildToken(user, DateTime.UtcNow.AddMinutes(_opt.AccessTokenMinutes));

    public (string token, DateTime expiresAtUtc) CreateDesktopToken(User user)
        => BuildToken(user, DateTime.UtcNow.AddDays(_opt.DesktopSessionDays));

    private (string token, DateTime expiresAtUtc) BuildToken(User user, DateTime expires)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opt.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _opt.Issuer,
            audience: _opt.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    /// <summary>Token opaco de 48 bytes (base64). É o valor bruto entregue ao cliente.</summary>
    public string GenerateRefreshToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

    /// <summary>SHA-256 (hex) do token bruto — é o que persistimos no banco.</summary>
    public string HashRefreshToken(string rawToken)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}
