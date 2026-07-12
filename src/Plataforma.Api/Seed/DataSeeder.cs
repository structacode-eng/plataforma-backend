using Plataforma.Application.Abstractions;
using Plataforma.Domain.Entities;
using Plataforma.Domain.Enums;

namespace Plataforma.Api.Seed;

/// <summary>
/// Garante que exista um usuário Owner inicial (você) a partir de configuração/segredo.
/// Idempotente: cria se não existir, promove a Owner se existir com outro papel.
/// É assim que o primeiro administrador nasce sem endpoint aberto de "criar admin".
/// </summary>
public static class DataSeeder
{
    public static async Task SeedOwnerAsync(IServiceProvider services, IConfiguration config, CancellationToken ct = default)
    {
        var email = config["Seed:Owner:Email"];
        var password = config["Seed:Owner:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return; // nada configurado → não faz nada

        using var scope = services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var normalized = User.Normalize(email);
        var existing = await users.GetByEmailAsync(normalized, ct);

        if (existing is null)
        {
            var owner = new User(normalized, hasher.Hash(password), UserRole.Owner);
            owner.MarkEmailVerified();
            await users.AddAsync(owner, ct);
            await uow.SaveChangesAsync(ct);
        }
        else if (existing.Role != UserRole.Owner)
        {
            existing.SetRole(UserRole.Owner);
            await uow.SaveChangesAsync(ct);
        }
    }
}
