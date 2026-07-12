namespace Plataforma.Domain.Entities;

/// <summary>
/// Item de catálogo: um módulo licenciável (Hidráulica, Elétrica, Clash Manager…).
/// O <c>Slug</c> é o identificador estável usado na toolbar, na API e no lease (RF-PLN-001).
/// </summary>
public sealed class Plugin
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Slug { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;

    private Plugin() { } // EF

    public Plugin(string slug, string name, string? description = null)
    {
        Slug = slug.Trim().ToLowerInvariant();
        Name = name;
        Description = description;
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
