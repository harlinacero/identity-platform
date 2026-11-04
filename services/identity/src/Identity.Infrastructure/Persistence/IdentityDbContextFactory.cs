using Identity.Application.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Identity.Infrastructure.Persistence;

/// <summary>Used by the EF Core tools. Migrations run as identity_migrator, with no tenant.</summary>
public sealed class IdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var connectionString = args.FirstOrDefault() ?? "Host=localhost;Database=identity";
        var options = new DbContextOptionsBuilder<IdentityDbContext>().UseNpgsql(connectionString).Options;
        return new IdentityDbContext(options, new TenantContext());
    }
}
