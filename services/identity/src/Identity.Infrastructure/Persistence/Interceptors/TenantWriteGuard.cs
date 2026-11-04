using Identity.Domain.Tenancy;

namespace Identity.Infrastructure.Persistence.Interceptors;

public static class TenantWriteGuard
{
    public static void Apply(ITenantOwned entity, Guid? tenantId)
    {
        if (tenantId is not { } current)
        {
            throw new InvalidOperationException("Tenant-owned data cannot be written without a tenant.");
        }

        if (entity.TenantId == Guid.Empty)
        {
            entity.TenantId = current;
        }
        else if (entity.TenantId != current)
        {
            throw new InvalidOperationException("Tenant-owned data cannot be written for another tenant.");
        }
    }
}
