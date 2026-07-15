using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Plataforma.Application.Releases;
using Plataforma.Domain.Entities;

namespace Plataforma.Api.Controllers;

/// <summary>
/// Endpoint para o pipeline (GitHub Actions) publicar uma build no canal CANÁRIO,
/// automaticamente. Autenticado por um token estático (header <c>X-Ci-Token</c>,
/// vindo de <c>Ci:PublishToken</c>) — NÃO usa a conta Owner. Escopo mínimo por design:
/// só publica no <b>canário</b>, nunca no estável. A promoção canário→estável é manual,
/// feita pelo Owner no painel — então um vazamento do token do CI não empurra nada
/// para todas as máquinas.
/// </summary>
[ApiController]
[Route("v1/ci")]
public sealed class CiController : ControllerBase
{
    private readonly ReleaseService _releases;
    private readonly IConfiguration _config;

    public CiController(ReleaseService releases, IConfiguration config)
    {
        _releases = releases;
        _config = config;
    }

    [HttpPost("release")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> PublishCanary(
        [FromBody] SetReleaseRequest req,
        [FromHeader(Name = "X-Ci-Token")] string? token,
        CancellationToken ct)
    {
        var expected = _config["Ci:PublishToken"];
        if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrEmpty(token) || token != expected)
            return Unauthorized(new { error = "Token de CI inválido.", code = "invalid_ci_token" });

        var r = await _releases.SetManifestAsync(req, ReleaseManifest.Canary, ct);
        if (r.Success) return Ok(r.Value);
        return StatusCode(StatusCodes.Status400BadRequest, new { error = r.Error, code = r.Code });
    }
}
