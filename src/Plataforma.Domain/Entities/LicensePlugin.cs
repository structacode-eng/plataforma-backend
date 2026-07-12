namespace Plataforma.Domain.Entities;

/// <summary>
/// "Retrato" dos módulos concedidos por uma licença (chave composta LicenseId + PluginSlug).
/// Guardamos o slug diretamente (denormalizado) para: (1) montar o lease sem JOIN e
/// (2) congelar o escopo vendido — alterar um plano depois não muda licenças já emitidas (RF-PLN-005).
/// </summary>
public sealed class LicensePlugin
{
    public Guid LicenseId { get; private set; }
    public string PluginSlug { get; private set; } = null!;

    private LicensePlugin() { } // EF

    public LicensePlugin(Guid licenseId, string pluginSlug)
    {
        LicenseId = licenseId;
        PluginSlug = pluginSlug.Trim().ToLowerInvariant();
    }
}
