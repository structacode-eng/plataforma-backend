using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Plataforma.Application.Common;
using Plataforma.Application.Licensing;

namespace Plataforma.Api.Controllers;

/// <summary>
/// Administração de catálogo e licenças (RF-ADM-003/004, RF-PLN-*, RF-LIC-001/004).
/// Classe restrita a Owner/Support; operações de escrita exigem Owner.
/// Reset de dispositivo (RF-DEV-004) fica acessível também ao Support.
/// </summary>
[ApiController]
[Route("v1/admin")]
[Authorize(Roles = "Owner,Support")]
public sealed class AdminController : ControllerBase
{
    private readonly AdminCatalogService _svc;
    public AdminController(AdminCatalogService svc) => _svc = svc;

    [HttpPost("plugins")]
    [Authorize(Roles = "Owner")]
    public async Task<IActionResult> CreatePlugin([FromBody] CreatePluginRequest req, CancellationToken ct)
        => Respond(await _svc.CreatePluginAsync(req, ct), StatusCodes.Status201Created);

    [HttpPost("plans")]
    [Authorize(Roles = "Owner")]
    public async Task<IActionResult> CreatePlan([FromBody] CreatePlanRequest req, CancellationToken ct)
        => Respond(await _svc.CreatePlanAsync(req, ct), StatusCodes.Status201Created);

    [HttpPost("licenses")]
    [Authorize(Roles = "Owner")]
    public async Task<IActionResult> IssueLicense([FromBody] IssueLicenseRequest req, CancellationToken ct)
        => Respond(await _svc.IssueLicenseAsync(req, ct), StatusCodes.Status201Created);

    [HttpPost("licenses/{id:guid}/revoke")]
    [Authorize(Roles = "Owner")]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct)
        => Respond(await _svc.RevokeLicenseAsync(id, ct));

    [HttpPost("licenses/{id:guid}/suspend")]
    [Authorize(Roles = "Owner")]
    public async Task<IActionResult> Suspend(Guid id, CancellationToken ct)
        => Respond(await _svc.SuspendLicenseAsync(id, ct));

    [HttpPost("licenses/{id:guid}/reactivate")]
    [Authorize(Roles = "Owner")]
    public async Task<IActionResult> Reactivate(Guid id, CancellationToken ct)
        => Respond(await _svc.ReactivateLicenseAsync(id, ct));

    [HttpPost("devices/{id:guid}/reset")]
    public async Task<IActionResult> ResetDevice(Guid id, CancellationToken ct)
        => Respond(await _svc.ResetDeviceAsync(id, ct));

    // Cria uma conta (cadastro público está fechado; só o Owner cria contas).
    [HttpPost("users")]
    [Authorize(Roles = "Owner")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest req, CancellationToken ct)
        => Respond(await _svc.CreateUserAsync(req?.Email, req?.Password, req?.Role, ct), StatusCodes.Status201Created);

    // Revoga/reativa o acesso de um usuário (login desktop). Owner apenas.
    [HttpPost("users/{email}/disable")]
    [Authorize(Roles = "Owner")]
    public async Task<IActionResult> DisableUser(string email, CancellationToken ct)
        => Respond(await _svc.DisableUserAsync(email, ct));

    [HttpPost("users/{email}/enable")]
    [Authorize(Roles = "Owner")]
    public async Task<IActionResult> EnableUser(string email, CancellationToken ct)
        => Respond(await _svc.EnableUserAsync(email, ct));

    private IActionResult Respond<T>(Result<T> r, int successCode = StatusCodes.Status200OK)
    {
        if (r.Success) return StatusCode(successCode, r.Value);
        var status = r.Code switch
        {
            "user_not_found" or "plan_not_found" or "plugin_not_found"
                or "license_not_found" or "device_not_found" => StatusCodes.Status404NotFound,
            "slug_taken" or "email_taken" => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        };
        return StatusCode(status, new { error = r.Error, code = r.Code });
    }
}
