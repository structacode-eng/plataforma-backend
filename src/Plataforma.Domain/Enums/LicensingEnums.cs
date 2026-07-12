namespace Plataforma.Domain.Enums;

/// <summary>Ciclo de cobrança de um plano/licença (RF-PLN-002).</summary>
public enum BillingCycle
{
    Monthly = 0,
    Annual = 1,
    Lifetime = 2
}

/// <summary>Estado de uma licença. Só <see cref="Active"/> concede módulos.</summary>
public enum LicenseStatus
{
    Active = 0,
    Suspended = 1, // ex.: pagamento atrasado (RF-LIC-005)
    Revoked = 2    // cancelamento/chargeback (RF-LIC-004) — definitivo
}

/// <summary>Origem do escopo da licença: um plano (pacote) ou um plugin avulso (RF-PLN-003).</summary>
public enum LicenseKind
{
    Plan = 0,
    Standalone = 1
}
