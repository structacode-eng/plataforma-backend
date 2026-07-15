using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Plataforma.Application.Common;
using Plataforma.Application.Licensing;
using Plataforma.Application.Releases;

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
    private readonly ReleaseService _releases;
    public AdminController(AdminCatalogService svc, ReleaseService releases)
    {
        _svc = svc;
        _releases = releases;
    }

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

    // Lista todas as contas (para o painel admin).
    [HttpGet("users")]
    [Authorize(Roles = "Owner")]
    public async Task<IActionResult> ListUsers([FromQuery] int limit, CancellationToken ct)
        => Respond(await _svc.ListUsersAsync(limit <= 0 ? 500 : limit, ct));

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

    // Redefine a senha de um usuário (Owner apenas). A pessoa passa a entrar com a nova senha.
    [HttpPost("users/{email}/reset-password")]
    [Authorize(Roles = "Owner")]
    public async Task<IActionResult> ResetPassword(string email, [FromBody] ResetPasswordRequest req, CancellationToken ct)
        => Respond(await _svc.ResetPasswordAsync(email, req?.Password, ct));

    // Altera o papel de um usuário (Customer/Support/Owner). Owner apenas.
    [HttpPut("users/{email}/role")]
    [Authorize(Roles = "Owner")]
    public async Task<IActionResult> SetRole(string email, [FromBody] SetRoleRequest req, CancellationToken ct)
        => Respond(await _svc.SetRoleAsync(email, req?.Role, ct));

    // Exclui uma conta (cascata: tokens/licenças/dispositivos). Owner apenas.
    [HttpDelete("users/{email}")]
    [Authorize(Roles = "Owner")]
    public async Task<IActionResult> DeleteUser(string email, CancellationToken ct)
        => Respond(await _svc.DeleteUserAsync(email, ct));

    // ----- Updater (Marco 5): manifesto de versão publicada -----
    [HttpGet("release")]
    [Authorize(Roles = "Owner")]
    public async Task<IActionResult> GetRelease(CancellationToken ct)
        => Ok(await _releases.GetBothAsync(ct));

    [HttpPut("release")]
    [Authorize(Roles = "Owner")]
    public async Task<IActionResult> SetRelease([FromBody] SetReleaseRequest req, [FromQuery] string? channel, CancellationToken ct)
        => Respond(await _releases.SetManifestAsync(req, channel, ct));

    // Promove a versão do canário para o estável (libera para TODAS as máquinas). Owner apenas.
    [HttpPost("release/promote")]
    [Authorize(Roles = "Owner")]
    public async Task<IActionResult> PromoteRelease(CancellationToken ct)
        => Respond(await _releases.PromoteAsync(ct));

    private IActionResult Respond<T>(Result<T> r, int successCode = StatusCodes.Status200OK)
    {
        if (r.Success) return StatusCode(successCode, r.Value);
        var status = r.Code switch
        {
            "user_not_found" or "plan_not_found" or "plugin_not_found"
                or "license_not_found" or "device_not_found" => StatusCodes.Status404NotFound,
            "slug_taken" or "email_taken" or "owner_protected" or "last_owner" => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        };
        return StatusCode(status, new { error = r.Error, code = r.Code });
    }
}
