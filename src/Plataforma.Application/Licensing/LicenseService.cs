using Plataforma.Application.Abstractions;
using Plataforma.Application.Common;
using Plataforma.Domain.Entities;

namespace Plataforma.Application.Licensing;

/// <summary>
/// Operações do cliente (plugin): ativar dispositivo, validar (receber o lease assinado),
/// listar o que possui e remover um dispositivo próprio (RF-LIC-002/003/006/007, RF-DEV-001/002/003).
/// O <c>userId</c> vem sempre do token autenticado — nunca do corpo da requisição.
/// </summary>
public sealed class LicenseService
{
    private readonly ILicenseRepository _licenses;
    private readonly IDeviceRepository _devices;
    private readonly ILeaseService _lease;
    private readonly IUnitOfWork _uow;

    public LicenseService(ILicenseRepository licenses, IDeviceRepository devices, ILeaseService lease, IUnitOfWork uow)
    {
        _licenses = licenses;
        _devices = devices;
        _lease = lease;
        _uow = uow;
    }

    public async Task<Result<ActivateResponse>> ActivateAsync(Guid userId, ActivateRequest req, CancellationToken ct = default)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.Fingerprint))
            return Result<ActivateResponse>.Fail("Fingerprint ausente.", "invalid_input");
        var fp = req.Fingerprint.Trim();

        var licenses = (await _licenses.ListByUserAsync(userId, ct)).Where(l => l.IsUsable).ToList();
        if (licenses.Count == 0)
            return Result<ActivateResponse>.Fail("Nenhuma licença ativa para este usuário.", "no_active_license");

        var items = new List<ActivateResultItem>();
        foreach (var lic in licenses)
        {
            var slugs = (await _licenses.GetPluginSlugsAsync(lic.Id, ct)).ToArray();

            var existing = await _devices.GetActiveByLicenseAndFingerprintAsync(lic.Id, fp, ct);
            if (existing is not null)
            {
                existing.Touch();
                items.Add(new ActivateResultItem(lic.Id, true, null, slugs));
                continue;
            }

            var count = await _devices.CountActiveByLicenseAsync(lic.Id, ct);
            if (count < lic.DeviceLimit)
            {
                await _devices.AddAsync(new Device(lic.Id, fp, req.DeviceName), ct);
                items.Add(new ActivateResultItem(lic.Id, true, null, slugs));
            }
            else
            {
                items.Add(new ActivateResultItem(lic.Id, false,
                    "Limite de dispositivos atingido — remova um computador para liberar vaga.", slugs));
            }
        }

        await _uow.SaveChangesAsync(ct);
        return Result<ActivateResponse>.Ok(new ActivateResponse(fp, items.ToArray()));
    }

    public async Task<Result<LeaseResponse>> ValidateAsync(Guid userId, ValidateRequest req, CancellationToken ct = default)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.Fingerprint))
            return Result<LeaseResponse>.Fail("Fingerprint ausente.", "invalid_input");
        var fp = req.Fingerprint.Trim();

        var licenses = (await _licenses.ListByUserAsync(userId, ct)).Where(l => l.IsUsable).ToList();
        var modules = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var lic in licenses)
        {
            var device = await _devices.GetActiveByLicenseAndFingerprintAsync(lic.Id, fp, ct);
            if (device is null) continue; // este dispositivo não está ativado nesta licença
            device.Touch();
            foreach (var s in await _licenses.GetPluginSlugsAsync(lic.Id, ct))
                modules.Add(s);
        }

        await _uow.SaveChangesAsync(ct);

        // Emite o lease mesmo que vazio: é uma declaração assinada e definitiva do estado.
        var result = _lease.Issue(userId, fp, modules.ToArray());
        return Result<LeaseResponse>.Ok(new LeaseResponse(result.Token, result.ExpiresAtUtc, result.Modules.ToArray()));
    }

    public async Task<Result<MyLicensesResponse>> GetMyLicensesAsync(Guid userId, CancellationToken ct = default)
    {
        var licenses = await _licenses.ListByUserAsync(userId, ct);
        var list = new List<MyLicenseDto>();
        foreach (var lic in licenses)
        {
            var slugs = (await _licenses.GetPluginSlugsAsync(lic.Id, ct)).ToArray();
            var devices = (await _devices.ListActiveByLicenseAsync(lic.Id, ct))
                .Select(d => new DeviceDto(d.Id, d.Fingerprint, d.Name, d.RegisteredAtUtc, d.LastSeenAtUtc))
                .ToArray();
            list.Add(new MyLicenseDto(lic.Id, lic.Kind.ToString(), lic.Status.ToString(), lic.ExpiresAtUtc, slugs, devices));
        }
        return Result<MyLicensesResponse>.Ok(new MyLicensesResponse(list.ToArray()));
    }

    /// <summary>Usuário remove um dispositivo da PRÓPRIA licença (RF-DEV-003).</summary>
    public async Task<Result<bool>> RemoveDeviceAsync(Guid userId, Guid deviceId, CancellationToken ct = default)
    {
        var device = await _devices.GetByIdAsync(deviceId, ct);
        if (device is null) return Result<bool>.Fail("Dispositivo não encontrado.", "device_not_found");

        var license = await _licenses.GetByIdAsync(device.LicenseId, ct);
        if (license is null || license.UserId != userId)
            return Result<bool>.Fail("Dispositivo não pertence a você.", "forbidden");

        device.Deactivate();
        await _uow.SaveChangesAsync(ct);
        return Result<bool>.Ok(true);
    }
}
