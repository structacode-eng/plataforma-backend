using Microsoft.EntityFrameworkCore;
using Plataforma.Application.Abstractions;
using Plataforma.Domain.Entities;

namespace Plataforma.Infrastructure.Persistence;

/// <summary>
/// Contexto EF Core. Também cumpre o papel de Unit of Work (SaveChangesAsync).
/// Mapeamento explícito: tabelas em snake_case, índices únicos, chaves compostas e FKs.
/// </summary>
public sealed class AppDbContext : DbContext, IUnitOfWork
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // Identidade (Marco 1)
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    // Catálogo, planos, licenças, dispositivos (Marco 2)
    public DbSet<Plugin> Plugins => Set<Plugin>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<PlanPlugin> PlanPlugins => Set<PlanPlugin>();
    public DbSet<License> Licenses => Set<License>();
    public DbSet<LicensePlugin> LicensePlugins => Set<LicensePlugin>();
    public DbSet<Device> Devices => Set<Device>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasKey(x => x.Id);
            e.Property(x => x.Email).HasMaxLength(320).IsRequired();
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.PasswordHash).IsRequired();
            e.Property(x => x.Role).HasConversion<int>();
            // defaultValue: true garante que usuários já existentes fiquem ATIVOS na migração.
            e.Property(x => x.IsActive).HasDefaultValue(true);
        });

        b.Entity<RefreshToken>(e =>
        {
            e.ToTable("refresh_tokens");
            e.HasKey(x => x.Id);
            e.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.Property(x => x.ReplacedByTokenHash).HasMaxLength(128);
            e.Ignore(x => x.IsActive);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Plugin>(e =>
        {
            e.ToTable("plugins");
            e.HasKey(x => x.Id);
            e.Property(x => x.Slug).HasMaxLength(80).IsRequired();
            e.HasIndex(x => x.Slug).IsUnique();
            e.Property(x => x.Name).HasMaxLength(160).IsRequired();
            e.Property(x => x.Description).HasMaxLength(1000);
        });

        b.Entity<Plan>(e =>
        {
            e.ToTable("plans");
            e.HasKey(x => x.Id);
            e.Property(x => x.Slug).HasMaxLength(80).IsRequired();
            e.HasIndex(x => x.Slug).IsUnique();
            e.Property(x => x.Name).HasMaxLength(160).IsRequired();
            e.Property(x => x.BillingCycle).HasConversion<int>();
        });

        b.Entity<PlanPlugin>(e =>
        {
            e.ToTable("plan_plugins");
            e.HasKey(x => new { x.PlanId, x.PluginId });
            e.HasOne<Plan>().WithMany().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Plugin>().WithMany().HasForeignKey(x => x.PluginId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<License>(e =>
        {
            e.ToTable("licenses");
            e.HasKey(x => x.Id);
            e.Property(x => x.Kind).HasConversion<int>();
            e.Property(x => x.Status).HasConversion<int>();
            e.HasIndex(x => x.UserId);
            e.Ignore(x => x.IsExpired);
            e.Ignore(x => x.IsUsable);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<LicensePlugin>(e =>
        {
            e.ToTable("license_plugins");
            e.HasKey(x => new { x.LicenseId, x.PluginSlug });
            e.Property(x => x.PluginSlug).HasMaxLength(80);
            e.HasOne<License>().WithMany().HasForeignKey(x => x.LicenseId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Device>(e =>
        {
            e.ToTable("devices");
            e.HasKey(x => x.Id);
            e.Property(x => x.Fingerprint).HasMaxLength(128).IsRequired();
            e.Property(x => x.Name).HasMaxLength(160);
            e.HasIndex(x => x.LicenseId);
            // Um fingerprint só pode estar ATIVO uma vez por licença (índice único parcial).
            e.HasIndex(x => new { x.LicenseId, x.Fingerprint })
             .IsUnique()
             .HasFilter("\"IsActive\" = true");
            e.HasOne<License>().WithMany().HasForeignKey(x => x.LicenseId).OnDelete(DeleteBehavior.Cascade);
        });

        base.OnModelCreating(b);
    }
}
