using Plataforma.Application.Abstractions;
using Plataforma.Application.Common;
using Plataforma.Domain.Entities;
using Plataforma.Domain.Enums;

namespace Plataforma.Application.Licensing;

/// <summary>
/// Operações administrativas de catálogo e licenças (RF-PLN-*, RF-LIC-001/004/005, RF-DEV-004).
/// É por aqui que você VENDE no início: emissão manual de licença, sem depender de gateway.
/// </summary>
public sealed class AdminCatalogService
{
    private readonly IPluginRepository _plugins;
    private readonly IPlanRepository _plans;
    private readonly ILicenseRepository _licenses;
    private readonly IUserRepository _users;
    private readonly IDeviceRepository _devices;
    private readonly IPasswordHasher _hasher;
    private readonly IUnitOfWork _uow;

    public AdminCatalogService(
        IPluginRepository plugins, IPlanRepository plans, ILicenseRepository licenses,
        IUserRepository users, IDeviceRepository devices, IPasswordHasher hasher, IUnitOfWork uow)
    {
        _plugins = plugins;
        _plans = plans;
        _licenses = licenses;
        _users = users;
        _devices = devices;
        _hasher = hasher;
        _uow = uow;
    }

    /// <summary>Cria uma conta (RF-AUTH-001 por ação administrativa). Cadastro público está fechado;
    /// só o Owner cria contas. A conta já nasce ativa e verificada.</summary>
    public async Task<Result<UserDto>> CreateUserAsync(string? email, string? password, string? role, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            return Result<UserDto>.Fail("E-mail inválido.", "invalid_email");
        if (string.IsNullOrEmpty(password) || password.Length < 8)
            return Result<UserDto>.Fail("A senha deve ter ao menos 8 caracteres.", "weak_password");

        var normalized = User.Normalize(email);
        if (await _users.ExistsByEmailAsync(normalized, ct))
            return Result<UserDto>.Fail("E-mail já cadastrado.", "email_taken");

        var userRole = ParseRole(role);
        var user = new User(normalized, _hasher.Hash(password), userRole);
        user.MarkEmailVerified(); // criada pelo Owner → já verificada
        await _users.AddAsync(user, ct);
        await _uow.SaveChangesAsync(ct);

        return Result<UserDto>.Ok(new UserDto(user.Email, userRole.ToString()));
    }

    private static UserRole ParseRole(string? role)
        => (!string.IsNullOrWhiteSpace(role) && Enum.TryParse<UserRole>(role, ignoreCase: true, out var r) && Enum.IsDefined(r))
            ? r : UserRole.Customer;

    public async Task<Result<PluginDto>> CreatePluginAsync(CreatePluginRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.Slug) || string.IsNullOrWhiteSpace(req.Name))
            return Result<PluginDto>.Fail("Slug e nome são obrigatórios.", "invalid_input");

        var slug = req.Slug.Trim().ToLowerInvariant();
        if (await _plugins.ExistsBySlugAsync(slug, ct))
            return Result<PluginDto>.Fail("Já existe um plugin com esse slug.", "slug_taken");

        var plugin = new Plugin(slug, req.Name, req.Description);
        await _plugins.AddAsync(plugin, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<PluginDto>.Ok(new PluginDto(plugin.Id, plugin.Slug, plugin.Name, plugin.IsActive));
    }

    public async Task<Result<PlanDto>> CreatePlanAsync(CreatePlanRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.Slug) || string.IsNullOrWhiteSpace(req.Name))
            return Result<PlanDto>.Fail("Slug e nome são obrigatórios.", "invalid_input");
        if (!TryParseCycle(req.BillingCycle, out var cycle))
            return Result<PlanDto>.Fail("BillingCycle inválido (Monthly, Annual ou Lifetime).", "invalid_cycle");
        if (req.DeviceLimit < 1)
            return Result<PlanDto>.Fail("DeviceLimit deve ser >= 1.", "invalid_input");

        var slug = req.Slug.Trim().ToLowerInvariant();
        if (await _plans.ExistsBySlugAsync(slug, ct))
            return Result<PlanDto>.Fail("Já existe um plano com esse slug.", "slug_taken");

        var resolved = new List<Plugin>();
        foreach (var s in (req.PluginSlugs ?? Array.Empty<string>()).Select(x => x.Trim().ToLowerInvariant()).Distinct())
        {
            var p = await _plugins.GetBySlugAsync(s, ct);
            if (p is null) return Result<PlanDto>.Fail($"Plugin '{s}' não existe no catálogo.", "plugin_not_found");
            resolved.Add(p);
        }
        if (resolved.Count == 0)
            return Result<PlanDto>.Fail("Um plano precisa de ao menos um plugin.", "invalid_input");

        var plan = new Plan(slug, req.Name, cycle, req.DeviceLimit);
        await _plans.AddAsync(plan, ct);
        foreach (var p in resolved)
            await _plans.AddPlanPluginAsync(new PlanPlugin(plan.Id, p.Id), ct);
        await _uow.SaveChangesAsync(ct);

        return Result<PlanDto>.Ok(new PlanDto(
            plan.Id, plan.Slug, plan.Name, plan.BillingCycle.ToString(), plan.DeviceLimit,
            resolved.Select(x => x.Slug).ToArray()));
    }

    public async Task<Result<LicenseDto>> IssueLicenseAsync(IssueLicenseRequest req, CancellationToken ct = default)
    {
        var user = await _users.GetByEmailAsync(User.Normalize(req.UserEmail ?? ""), ct);
        if (user is null) return Result<LicenseDto>.Fail("Usuário não encontrado.", "user_not_found");

        LicenseKind kind;
        Guid? planId = null;
        int deviceLimit;
        BillingCycle cycle;
        List<string> slugs;

        if (!string.IsNullOrWhiteSpace(req.PlanSlug))
        {
            var plan = await _plans.GetBySlugAsync(req.PlanSlug!.Trim().ToLowerInvariant(), ct);
            if (plan is null) return Result<LicenseDto>.Fail("Plano não encontrado.", "plan_not_found");
            kind = LicenseKind.Plan;
            planId = plan.Id;
            deviceLimit = req.DeviceLimit ?? plan.DeviceLimit;
            cycle = plan.BillingCycle;
            slugs = (await _plans.GetPluginSlugsAsync(plan.Id, ct)).ToList();
            if (slugs.Count == 0) return Result<LicenseDto>.Fail("Plano sem plugins.", "empty_plan");
        }
        else if (!string.IsNullOrWhiteSpace(req.PluginSlug))
        {
            var slug = req.PluginSlug!.Trim().ToLowerInvariant();
            var plugin = await _plugins.GetBySlugAsync(slug, ct);
            if (plugin is null) return Result<LicenseDto>.Fail("Plugin não encontrado.", "plugin_not_found");
            kind = LicenseKind.Standalone;
            deviceLimit = req.DeviceLimit ?? 1;
            if (!TryParseCycle(req.BillingCycle ?? "Monthly", out cycle))
                return Result<LicenseDto>.Fail("BillingCycle inválido.", "invalid_cycle");
            slugs = new List<string> { slug };
        }
        else
        {
            return Result<LicenseDto>.Fail("Informe PlanSlug (plano) ou PluginSlug (avulso).", "invalid_input");
        }

        if (deviceLimit < 1) return Result<LicenseDto>.Fail("DeviceLimit deve ser >= 1.", "invalid_input");

        DateTime? expires = req.ExpiresInDays.HasValue
            ? DateTime.UtcNow.AddDays(req.ExpiresInDays.Value)
            : cycle switch
            {
                BillingCycle.Monthly => DateTime.UtcNow.AddDays(30),
                BillingCycle.Annual => DateTime.UtcNow.AddDays(365),
                _ => (DateTime?)null // Lifetime
            };

        var license = new License(user.Id, kind, deviceLimit, expires, planId);
        await _licenses.AddAsync(license, ct);
        foreach (var s in slugs.Distinct())
            await _licenses.AddLicensePluginAsync(new LicensePlugin(license.Id, s), ct);
        await _uow.SaveChangesAsync(ct);

        return Result<LicenseDto>.Ok(new LicenseDto(
            license.Id, user.Email, kind.ToString(), license.Status.ToString(),
            deviceLimit, expires, slugs.ToArray()));
    }

    public Task<Result<bool>> RevokeLicenseAsync(Guid licenseId, CancellationToken ct = default)
        => ChangeStatusAsync(licenseId, l => l.Revoke(), ct);

    public Task<Result<bool>> SuspendLicenseAsync(Guid licenseId, CancellationToken ct = default)
        => ChangeStatusAsync(licenseId, l => l.Suspend(), ct);

    public Task<Result<bool>> ReactivateLicenseAsync(Guid licenseId, CancellationToken ct = default)
        => ChangeStatusAsync(licenseId, l => l.Reactivate(), ct);

    /// <summary>Suporte libera uma vaga de dispositivo (RF-DEV-004).</summary>
    public async Task<Result<bool>> ResetDeviceAsync(Guid deviceId, CancellationToken ct = default)
    {
        var device = await _devices.GetByIdAsync(deviceId, ct);
        if (device is null) return Result<bool>.Fail("Dispositivo não encontrado.", "device_not_found");
        device.Deactivate();
        await _uow.SaveChangesAsync(ct);
        return Result<bool>.Ok(true);
    }

    /// <summary>Revoga o acesso de um usuário (RF-LIC-004): bloqueia login e /auth/me.</summary>
    public async Task<Result<bool>> DisableUserAsync(string email, CancellationToken ct = default)
    {
        var user = await _users.GetByEmailAsync(User.Normalize(email ?? ""), ct);
        if (user is null) return Result<bool>.Fail("Usuário não encontrado.", "user_not_found");
        user.Deactivate();
        await _uow.SaveChangesAsync(ct);
        return Result<bool>.Ok(true);
    }

    public async Task<Result<bool>> EnableUserAsync(string email, CancellationToken ct = default)
    {
        var user = await _users.GetByEmailAsync(User.Normalize(email ?? ""), ct);
        if (user is null) return Result<bool>.Fail("Usuário não encontrado.", "user_not_found");
        user.Activate();
        await _uow.SaveChangesAsync(ct);
        return Result<bool>.Ok(true);
    }

    private async Task<Result<bool>> ChangeStatusAsync(Guid licenseId, Action<License> action, CancellationToken ct)
    {
        var license = await _licenses.GetByIdAsync(licenseId, ct);
        if (license is null) return Result<bool>.Fail("Licença não encontrada.", "license_not_found");
        action(license);
        await _uow.SaveChangesAsync(ct);
        return Result<bool>.Ok(true);
    }

    private static bool TryParseCycle(string? s, out BillingCycle cycle)
        => Enum.TryParse(s, ignoreCase: true, out cycle) && Enum.IsDefined(cycle);
}
