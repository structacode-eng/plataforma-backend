using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Plataforma.Application.Abstractions;
using Plataforma.Infrastructure.Persistence;
using Plataforma.Infrastructure.Persistence.Repositories;
using Plataforma.Infrastructure.Security;

namespace Plataforma.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var conn = config.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:Default não configurada. Rode: dotnet user-secrets set \"ConnectionStrings:Default\" \"...\"");

        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(conn));

        // O próprio DbContext é o Unit of Work.
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());

        // Identidade (Marco 1)
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddSingleton<IPasswordHasher, Argon2PasswordHasher>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.Configure<JwtOptions>(config.GetSection("Jwt"));

        // Licenciamento (Marco 2)
        services.AddScoped<IPluginRepository, PluginRepository>();
        services.AddScoped<IPlanRepository, PlanRepository>();
        services.AddScoped<ILicenseRepository, LicenseRepository>();
        services.AddScoped<IDeviceRepository, DeviceRepository>();
        // Release/updater (Marco 5)
        services.AddScoped<IReleaseRepository, ReleaseRepository>();
        // Singleton: a chave de assinatura do lease é carregada uma vez e reutilizada (ver EcdsaLeaseService).
        services.AddSingleton<ILeaseService, EcdsaLeaseService>();
        services.Configure<LeaseOptions>(config.GetSection("Lease"));

        return services;
    }
}
