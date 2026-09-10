using Plataforma.Application.Telemetry;

namespace Plataforma.Application.Abstractions;

/// <summary>
/// Transforma o relatório de uso num PDF pronto para download. A implementação
/// vive na Infraestrutura porque depende de uma biblioteca de renderização;
/// a camada de aplicação só conhece "relatório entra, bytes saem".
/// </summary>
public interface IUsageReportPdf
{
    byte[] Gerar(UsoRelatorioDto relatorio);
}
