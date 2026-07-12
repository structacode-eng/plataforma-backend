using Plataforma.Domain.Enums;

namespace Plataforma.Domain.Entities;

/// <summary>
/// Plano: agrupa plugins e define política de cobrança e limite de dispositivos (RF-PLN-002).
/// A composição (quais plugins) vive na tabela plan_plugins; ao emitir uma licença de plano,
/// tiramos um "retrato" dessa composição para não afetar licenças já vendidas (RF-PLN-005).
/// </summary>
public sealed class Plan
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Slug { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public BillingCycle BillingCycle { get; private set; }
    public int DeviceLimit { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;

    private Plan() { } // EF

    public Plan(string slug, string name, BillingCycle billingCycle, int deviceLimit)
    {
        Slug = slug.Trim().ToLowerInvariant();
        Name = name;
        BillingCycle = billingCycle;
        DeviceLimit = deviceLimit;
    }
}
