namespace Plataforma.Domain.Entities;

/// <summary>Composição de um plano: relação N:N entre Plan e Plugin (chave composta).</summary>
public sealed class PlanPlugin
{
    public Guid PlanId { get; private set; }
    public Guid PluginId { get; private set; }

    private PlanPlugin() { } // EF

    public PlanPlugin(Guid planId, Guid pluginId)
    {
        PlanId = planId;
        PluginId = pluginId;
    }
}
