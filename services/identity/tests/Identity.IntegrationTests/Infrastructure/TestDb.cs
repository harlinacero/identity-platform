using Identity.Application.Tenancy;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;

namespace Identity.IntegrationTests.Infrastructure;

public static class TestDb
{
    public static readonly Guid TenantA = Guid.Parse("aaaaaaaa-0000-4000-8000-000000000001");
    public static readonly Guid TenantB = Guid.Parse("bbbbbbbb-0000-4000-8000-000000000002");

    /// <summary>A context wired as in the service: runtime role, with both interceptors.</summary>
    public static IdentityDbContext CreateContext(string connectionString, Guid? tenantId)
    {
        var tenantContext = new TenantContext();
        if (tenantId is { } id)
        {
            tenantContext.Set(id);
        }

        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(connectionString)
            .AddInterceptors(new TenantConnectionInterceptor(tenantContext), new TenantWriteInterceptor(tenantContext))
            .Options;

        return new IdentityDbContext(options, tenantContext);
    }
}
