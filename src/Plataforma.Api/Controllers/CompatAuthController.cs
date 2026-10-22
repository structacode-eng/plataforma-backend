using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Plataforma.Api.Auth;
using Plataforma.Application.Auth;

namespace Plataforma.Api.Controllers;

/// <summary>
/// Camada de compatibilidade para o cliente desktop (plugin Filippon), que espera o
/// contrato fixo: POST /auth/login, GET /auth/me, GET /health — fora do versionamento /v1
/// e com nomes de campo em snake_case. Mantém o plugin sem alterações de código.
/// </summary>
[ApiController]
public sealed class CompatAuthController : ControllerBase
{
    /// <summary>
    /// Cabeçalho com o slug do produto que está chamando ("revit-plugin",
    /// "solutions"). Os dois produtos usam as mesmas contas; sem isto, os
    /// contadores do <c>User</c> misturam o uso dos dois e não há como separar.
    ///
    /// <para>É informativo, não uma credencial: nada é autorizado com base
    /// nele, e valor desconhecido cai em "desconhecido" na normalização.</para>
    /// </summary>
    public const string HeaderProduto = "X-Filippon-Product";

    private readonly DesktopAuthService _svc;
    private readonly ILogger<CompatAuthController> _log;

    public CompatAuthController(DesktopAuthService svc, ILogger<CompatAuthController> log)
    {
        _svc = svc;
        _log = log;
    }

    private string? Produto()
        => Request.Headers.TryGetValue(HeaderProduto, out var v) ? v.ToString() : null;

    [HttpPost("/auth/login")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login([FromBody] DesktopLoginRequest req, CancellationToken ct)
    {
        var r = await _svc.LoginAsync(req?.Email, req?.Password, Produto(), ct);
        if (!r.Success) return Unauthorized(new { error = r.Error, code = r.Code });

        var s = r.Value!;
        return Ok(new DesktopLoginResponse
        {
            AccessToken = s.Token,
            TokenType = "Bearer",
            ExpiresAt = s.ExpiresAtUtc.ToString("o"), // ISO 8601 UTC
            User = new DesktopUserDto { Email = s.Email, Name = s.Name }
        });
    }

    [Authorize]
    [HttpGet("/auth/me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var me = await _svc.GetMeAsync(User.GetUserId(), Produto(), tokenExpiresAtUtc: VencimentoDoToken(), ct: ct);
        if (me is null) return Unauthorized();

        if (me.HeartbeatFailure is not null)
            _log.LogWarning(me.HeartbeatFailure, "auth/me: falha ao gravar o sinal de vida; validação seguiu.");

        return Ok(new DesktopMeResponse
        {
            Email = me.Email,
            Name = me.Name,
            Active = me.Active,
            AccessToken = me.RenewedToken,
            TokenType = me.RenewedToken is null ? null : "Bearer",
            ExpiresAt = me.RenewedExpiresAtUtc?.ToString("o"),
        });
    }

    /// <summary>Vencimento do token desta requisição (claim <c>exp</c>, segundos Unix).</summary>
    private DateTime? VencimentoDoToken()
    {
        var exp = User.FindFirst("exp")?.Value;
        return long.TryParse(exp, out var s) ? DateTimeOffset.FromUnixTimeSeconds(s).UtcDateTime : null;
    }

    [HttpGet("/health")]
    public IActionResult Health() => Ok(new { status = "ok" });
}

// ----- Contrato do cliente desktop (snake_case explícito) -----

public sealed class DesktopLoginRequest
{
    public string? Email { get; set; }
    public string? Password { get; set; }
}

public sealed class DesktopLoginResponse
{
    [JsonPropertyName("access_token")] public string AccessToken { get; set; } = "";
    [JsonPropertyName("token_type")] public string TokenType { get; set; } = "Bearer";
    [JsonPropertyName("expires_at")] public string ExpiresAt { get; set; } = "";
    [JsonPropertyName("user")] public DesktopUserDto? User { get; set; }
}

public sealed class DesktopUserDto
{
    [JsonPropertyName("email")] public string Email { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
}

public sealed class DesktopMeResponse
{
    [JsonPropertyName("email")] public string Email { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("active")] public bool Active { get; set; }

    // Renovação do login (desde 21/10/2026). Só aparecem quando há token novo, e só
    // para o plugin do Revit; mesmos nomes do /auth/login. Quem não conhece ignora.
    [JsonPropertyName("access_token"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AccessToken { get; set; }

    [JsonPropertyName("token_type"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TokenType { get; set; }

    [JsonPropertyName("expires_at"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ExpiresAt { get; set; }
}
