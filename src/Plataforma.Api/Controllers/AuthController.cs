using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Plataforma.Application.Auth;
using Plataforma.Application.Common;

namespace Plataforma.Api.Controllers;

/// <summary>Endpoints de identidade (RF-AUTH-001/003/004/005).</summary>
[ApiController]
[Route("v1/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly AuthService _auth;
    public AuthController(AuthService auth) => _auth = auth;

    // Cadastro público FECHADO — somente o Owner cria contas (POST /v1/admin/users).
    [HttpPost("register")]
    public IActionResult Register()
        => StatusCode(StatusCodes.Status403Forbidden,
            new { error = "Cadastro fechado. Contate o administrador.", code = "registration_closed" });

    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req, CancellationToken ct)
        => ToResponse(await _auth.LoginAsync(req, ct));

    [HttpPost("refresh")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest req, CancellationToken ct)
        => ToResponse(await _auth.RefreshAsync(req, ct));

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest req, CancellationToken ct)
    {
        var r = await _auth.LogoutAsync(req, ct);
        return r.Success ? NoContent() : BadRequest(new { error = r.Error, code = r.Code });
    }

    private IActionResult ToResponse(Result<TokenResponse> r, int successCode = StatusCodes.Status200OK)
    {
        if (r.Success) return StatusCode(successCode, r.Value);

        var status = r.Code switch
        {
            "email_taken" => StatusCodes.Status409Conflict,
            "invalid_credentials" or "invalid_token" => StatusCodes.Status401Unauthorized,
            _ => StatusCodes.Status400BadRequest
        };
        return StatusCode(status, new { error = r.Error, code = r.Code });
    }
}
