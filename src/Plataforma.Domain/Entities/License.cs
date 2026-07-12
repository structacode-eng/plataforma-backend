using Plataforma.Domain.Enums;

namespace Plataforma.Domain.Entities;

/// <summary>
/// Licença emitida a um usuário (RF-LIC-001). Define escopo (via license_plugins),
/// validade (<c>ExpiresAtUtc</c> nulo = vitalícia) e limite de dispositivos.
/// Só concede módulos quando <see cref="IsUsable"/> (ativa e não expirada).
/// </summary>
public sealed class License
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public LicenseKind Kind { get; private set; }
    public Guid? PlanId { get; private set; }               // preenchido quando Kind = Plan
    public LicenseStatus Status { get; private set; } = LicenseStatus.Active;
    public int DeviceLimit { get; private set; }
    public DateTime IssuedAtUtc { get; private set; } = DateTime.UtcNow;
    public DateTime? ExpiresAtUtc { get; private set; }     // null = vitalícia
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; private set; }

    private License() { } // EF

    public License(Guid userId, LicenseKind kind, int deviceLimit, DateTime? expiresAtUtc, Guid? planId = null)
    {
        UserId = userId;
        Kind = kind;
        DeviceLimit = deviceLimit;
        ExpiresAtUtc = expiresAtUtc;
        PlanId = planId;
    }

    public bool IsExpired => ExpiresAtUtc is not null && DateTime.UtcNow >= ExpiresAtUtc;
    public bool IsUsable => Status == LicenseStatus.Active && !IsExpired;

    public void Revoke() { Status = LicenseStatus.Revoked; Touch(); }
    public void Suspend() { if (Status == LicenseStatus.Active) { Status = LicenseStatus.Suspended; Touch(); } }
    public void Reactivate() { if (Status == LicenseStatus.Suspended) { Status = LicenseStatus.Active; Touch(); } }

    private void Touch() => UpdatedAtUtc = DateTime.UtcNow;
}
