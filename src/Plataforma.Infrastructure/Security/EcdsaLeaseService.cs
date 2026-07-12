using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Plataforma.Application.Abstractions;

namespace Plataforma.Infrastructure.Security;

public sealed class LeaseOptions
{
    /// <summary>Chave privada ECDSA P-256 em base64 (PKCS#8). Vem de segredo — nunca do appsettings versionado.</summary>
    public string PrivateKey { get; set; } = "";
    public string Issuer { get; set; } = "plataforma-lease";
    public int LeaseHours { get; set; } = 24;
}

/// <summary>
/// Emite o lease como um JWT assinado em <b>ES256</b> (ECDSA P-256 + SHA-256).
/// Só o servidor tem a chave privada; o plugin verifica com a pública embutida.
/// Os módulos vão no claim <c>mods</c> como array JSON (sempre array, mesmo com 0/1 item).
///
/// Registrado como SINGLETON: a chave é importada uma vez e vive pela aplicação inteira.
/// O provedor de assinatura do Microsoft.IdentityModel é cacheado e reutilizado com segurança
/// entre chamadas — descartar a chave por requisição causaria ObjectDisposedException na 2ª emissão.
/// </summary>
public sealed class EcdsaLeaseService : ILeaseService
{
    private readonly LeaseOptions _opt;
    private readonly SigningCredentials _credentials;

    public EcdsaLeaseService(IOptions<LeaseOptions> opt)
    {
        _opt = opt.Value;
        if (string.IsNullOrWhiteSpace(_opt.PrivateKey))
            throw new InvalidOperationException("Lease:PrivateKey não configurada (dotnet user-secrets).");

        var ecdsa = ECDsa.Create(); // vive pela aplicação (serviço singleton) — não descartar
        ecdsa.ImportPkcs8PrivateKey(Convert.FromBase64String(_opt.PrivateKey), out _);
        _credentials = new SigningCredentials(new ECDsaSecurityKey(ecdsa), SecurityAlgorithms.EcdsaSha256);
    }

    public LeaseResult Issue(Guid userId, string fingerprint, IReadOnlyCollection<string> modules)
    {
        var now = DateTime.UtcNow;
        var expires = now.AddHours(_opt.LeaseHours);

        var modsArray = modules.ToArray();
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new("fp", fingerprint),
            new("mods", JsonSerializer.Serialize(modsArray), JsonClaimValueTypes.JsonArray)
        };

        var token = new JwtSecurityToken(
            issuer: _opt.Issuer,
            audience: null,
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: _credentials);

        var jwt = new JwtSecurityTokenHandler().WriteToken(token);
        return new LeaseResult(jwt, expires, modsArray);
    }
}
