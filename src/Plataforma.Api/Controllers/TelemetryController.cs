using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Plataforma.Api.Auth;
using Plataforma.Application.Telemetry;

namespace Plataforma.Api.Controllers;

/// <summary>
/// Recebe o uso de ferramentas enviado pelo plugin, em lote.
///
/// <para>O plugin acumula localmente e envia ao fechar o Revit — nunca a cada
/// clique. Se o envio falhar, a fila fica no disco dele e vai na próxima, então
/// este endpoint precisa aguentar receber dias acumulados de uma vez.</para>
/// </summary>
[ApiController]
[Authorize]
public sealed class TelemetryController : ControllerBase
{
    private readonly TelemetryService _svc;
    public TelemetryController(TelemetryService svc) => _svc = svc;

    private string? Produto()
        => Request.Headers.TryGetValue(CompatAuthController.HeaderProduto, out var v) ? v.ToString() : null;

    [HttpPost("/v1/telemetry")]
    [EnableRateLimiting("telemetry")]
    public async Task<IActionResult> Registrar([FromBody] TelemetryRequest? req, CancellationToken ct)
    {
        var r = await _svc.RegistrarAsync(User.GetUserId(), Produto(), req?.Eventos, ct);
        return Ok(new { aceitos = r.Aceitos, descartados = r.Descartados });
    }
}

public sealed class TelemetryRequest
{
    public List<UsoEvento>? Eventos { get; set; }
}
