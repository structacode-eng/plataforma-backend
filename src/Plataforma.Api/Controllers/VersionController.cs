using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Plataforma.Application.Releases;

namespace Plataforma.Api.Controllers;

/// <summary>
/// Manifesto de versão para o auto-update do plugin (Marco 5). Contrato fixo esperado
/// pelo Filippon (UpdateService): <c>GET /version</c> → <c>{latest,url,notes,mandatory,sha256}</c>.
/// Público (sem auth): o plugin consulta no boot, antes de logar.
/// </summary>
[ApiController]
public sealed class VersionController : ControllerBase
{
    private readonly ReleaseService _svc;
    public VersionController(ReleaseService svc) => _svc = svc;

    /// <summary>Cabeçalho que identifica o produto chamador. Mesmo usado no login.</summary>
    public const string HeaderProduto = "X-Filippon-Product";

    [HttpGet("/version")]
    public async Task<IActionResult> Version([FromQuery] string? channel, CancellationToken ct)
    {
        // SEM cabeçalho o manifesto é o do plugin do Revit. É isto que mantém a
        // frota já instalada intacta: o UpdateService em campo não manda o
        // cabeçalho e continua recebendo exatamente o que sempre recebeu.
        var produto = Request.Headers.TryGetValue(HeaderProduto, out var v) ? v.ToString() : null;
        var m = await _svc.GetManifestAsync(produto, channel, ct);
        return Ok(new VersionResponse
        {
            Latest = m.Latest,
            Url = m.Url ?? "",
            Notes = m.Notes ?? "",
            Mandatory = m.Mandatory,
            Sha256 = m.Sha256 ?? ""
        });
    }
}

/// <summary>Contrato snake_case/lower que o UpdateService do plugin parseia.</summary>
public sealed class VersionResponse
{
    [JsonPropertyName("latest")] public string Latest { get; set; } = "0.0.0";
    [JsonPropertyName("url")] public string Url { get; set; } = "";
    [JsonPropertyName("notes")] public string Notes { get; set; } = "";
    [JsonPropertyName("mandatory")] public bool Mandatory { get; set; }
    [JsonPropertyName("sha256")] public string Sha256 { get; set; } = "";
}
