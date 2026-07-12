using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Plataforma.Api.Auth;
using Plataforma.Application.Common;
using Plataforma.Application.Licensing;

namespace Plataforma.Api.Controllers;

/// <summary>Endpoints consumidos pelo plugin (RF-LIC-002/007, RF-DEV-001/002/003). Exigem autenticação.</summary>
[ApiController]
[Route("v1/license")]
[Authorize]
public sealed class LicenseController : ControllerBase
{
    private readonly LicenseService _svc;
    public LicenseController(LicenseService svc) => _svc = svc;

    /// <summary>Registra o dispositivo (fingerprint) nas licenças ativas do usuário, respeitando o limite.</summary>
    [HttpPost("activate")]
    public async Task<IActionResult> Activate([FromBody] ActivateRequest req, CancellationToken ct)
        => Respond(await _svc.ActivateAsync(User.GetUserId(), req, ct));

    /// <summary>Devolve o lease assinado (módulos liberados para este dispositivo, validade 24h).</summary>
    [HttpPost("validate")]
    public async Task<IActionResult> Validate([FromBody] ValidateRequest req, CancellationToken ct)
        => Respond(await _svc.ValidateAsync(User.GetUserId(), req, ct));

    /// <summary>Estado das licenças do próprio usuário (RF-LIC-006).</summary>
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
        => Respond(await _svc.GetMyLicensesAsync(User.GetUserId(), ct));

    /// <summary>Usuário libera uma vaga removendo um dispositivo próprio (RF-DEV-003).</summary>
    [HttpDelete("devices/{id:guid}")]
    public async Task<IActionResult> RemoveDevice(Guid id, CancellationToken ct)
    {
        var r = await _svc.RemoveDeviceAsync(User.GetUserId(), id, ct);
        return r.Success ? NoContent() : Error(r.Error, r.Code);
    }

    private IActionResult Respond<T>(Result<T> r)
        => r.Success ? Ok(r.Value) : Error(r.Error, r.Code);

    private IActionResult Error(string? error, string? code)
    {
        var status = code switch
        {
            "no_active_license" or "forbidden" => StatusCodes.Status403Forbidden,
            "device_not_found" => StatusCodes.Status404NotFound,
            _ => StatusCodes.Status400BadRequest
        };
        return StatusCode(status, new { error, code });
    }
}
