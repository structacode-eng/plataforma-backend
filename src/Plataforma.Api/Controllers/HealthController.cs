using Microsoft.AspNetCore.Mvc;

namespace Plataforma.Api.Controllers;

/// <summary>
/// Endpoint de verificação de saúde da API (health check).
/// Não depende de banco — serve para o balanceador/monitoramento confirmar
/// que a instância está viva (base para RNF-AVAIL e os health checks do Cap. 18).
/// </summary>
[ApiController]
[Route("v1/[controller]")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new
    {
        status = "ok",
        service = "Plataforma.Api",
        utc = DateTime.UtcNow
    });
}
