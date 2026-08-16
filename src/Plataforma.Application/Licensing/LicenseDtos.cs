namespace Plataforma.Application.Licensing;

// ---------- Admin (emissão/catálogo) ----------
public record CreateUserRequest(string Email, string Password, string? Role);
public record ResetPasswordRequest(string Password);
public record SetRoleRequest(string Role);
public record UserDto(
    string Email, string Role, bool IsActive, DateTime CreatedAtUtc,
    DateTime? LastSeenAtUtc, int LoginCount,
    /// <summary>Uso por produto. Vazio = conta que ainda não abriu nada desde
    /// que o rastreio por produto entrou (LoginCount pode ser > 0 e esta lista
    /// vazia: o contador antigo é a soma histórica de todos os produtos).</summary>
    IReadOnlyList<ProdutoUsoDto>? Produtos = null);

/// <summary>Uso de UM produto por um usuário.</summary>
public record ProdutoUsoDto(string Produto, DateTime PrimeiroAcessoUtc, DateTime UltimoAcessoUtc, int Logins);
public record CreatePluginRequest(string Slug, string Name, string? Description);
public record CreatePlanRequest(string Slug, string Name, string BillingCycle, int DeviceLimit, string[] PluginSlugs);
public record IssueLicenseRequest(
    string UserEmail,
    string? PlanSlug,
    string? PluginSlug,
    string? BillingCycle,
    int? DeviceLimit,
    int? ExpiresInDays);

public record PluginDto(Guid Id, string Slug, string Name, bool IsActive);
public record PlanDto(Guid Id, string Slug, string Name, string BillingCycle, int DeviceLimit, string[] Plugins);
public record LicenseDto(Guid Id, string UserEmail, string Kind, string Status, int DeviceLimit, DateTime? ExpiresAtUtc, string[] Modules);

// ---------- Cliente (plugin) ----------
public record ActivateRequest(string Fingerprint, string? DeviceName);
public record ValidateRequest(string Fingerprint);

public record ActivateResultItem(Guid LicenseId, bool Activated, string? Reason, string[] Modules);
public record ActivateResponse(string Fingerprint, ActivateResultItem[] Licenses);

public record LeaseResponse(string Lease, DateTime ExpiresAtUtc, string[] Modules);

public record DeviceDto(Guid Id, string Fingerprint, string? Name, DateTime RegisteredAtUtc, DateTime? LastSeenAtUtc);
public record MyLicenseDto(Guid Id, string Kind, string Status, DateTime? ExpiresAtUtc, string[] Modules, DeviceDto[] Devices);
public record MyLicensesResponse(MyLicenseDto[] Licenses);
