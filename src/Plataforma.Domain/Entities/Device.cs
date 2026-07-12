namespace Plataforma.Domain.Entities;

/// <summary>
/// Dispositivo (máquina) ativado sob uma licença (RF-DEV-001). O <c>Fingerprint</c> é um
/// identificador estável de hardware/OS coletado pelo plugin. A desativação é "soft"
/// (IsActive=false) para preservar histórico de auditoria.
/// </summary>
public sealed class Device
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid LicenseId { get; private set; }
    public string Fingerprint { get; private set; } = null!;
    public string? Name { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime RegisteredAtUtc { get; private set; } = DateTime.UtcNow;
    public DateTime? LastSeenAtUtc { get; private set; }

    private Device() { } // EF

    public Device(Guid licenseId, string fingerprint, string? name = null)
    {
        LicenseId = licenseId;
        Fingerprint = fingerprint;
        Name = name;
    }

    public void Touch() => LastSeenAtUtc = DateTime.UtcNow;
    public void Deactivate() => IsActive = false;
}
