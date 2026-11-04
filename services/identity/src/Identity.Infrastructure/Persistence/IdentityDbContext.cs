using System.Text.RegularExpressions;
using Identity.Application.Tenancy;
using Identity.Domain.Users;
using Identity.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence;

public sealed partial class IdentityDbContext(DbContextOptions<IdentityDbContext> options, ITenantContext tenantContext)
    : DbContext(options)
{
    private Guid? CurrentTenantId => tenantContext.TenantId;

    public DbSet<LocalUser> LocalUsers => Set<LocalUser>();

    public DbSet<ClientDirectoryEntry> ClientDirectory => Set<ClientDirectoryEntry>();

    public DbSet<TenantApplication> Applications => Set<TenantApplication>();

    public DbSet<TenantAuthorization> Authorizations => Set<TenantAuthorization>();

    public DbSet<TenantToken> Tokens => Set<TenantToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.UseOpenIddict<TenantApplication, TenantAuthorization, PlatformScope, TenantToken, Guid>();

        modelBuilder.Entity<LocalUser>(user =>
        {
            user.ToTable("local_users");
            user.HasKey(entity => entity.Id);
            user.Property(entity => entity.Username).HasMaxLength(256);
            user.Property(entity => entity.NormalizedUsername).HasMaxLength(256);
            user.HasIndex(entity => new { entity.TenantId, entity.NormalizedUsername }).IsUnique();
            user.HasQueryFilter(entity => entity.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<ClientDirectoryEntry>(entry =>
        {
            entry.ToTable("client_directory");
            entry.HasKey(entity => entity.ClientId);
            entry.Property(entity => entity.ClientId).HasMaxLength(100);
        });

        modelBuilder.Entity<TenantApplication>(application =>
        {
            application.ToTable("openiddict_applications");
            application.HasQueryFilter(entity => entity.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<TenantAuthorization>(authorization =>
        {
            authorization.ToTable("openiddict_authorizations");
            authorization.HasQueryFilter(entity => entity.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<TenantToken>(token =>
        {
            token.ToTable("openiddict_tokens");
            token.HasQueryFilter(entity => entity.TenantId == CurrentTenantId);
        });

        modelBuilder.Entity<PlatformScope>().ToTable("openiddict_scopes");

        ApplySnakeCaseColumns(modelBuilder);
    }

    private static void ApplySnakeCaseColumns(ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
            {
                property.SetColumnName(UpperCaseBoundary().Replace(property.Name, "$1_$2").ToLowerInvariant());
            }
        }
    }

    [GeneratedRegex("([a-z0-9])([A-Z])")]
    private static partial Regex UpperCaseBoundary();
}
