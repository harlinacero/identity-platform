namespace Identity.Application.Tenancy;

public sealed class TenantContext : ITenantContext, ITenantContextSetter
{
    public Guid? TenantId { get; private set; }

    public void Set(Guid tenantId)
    {
        if (TenantId is { } current && current != tenantId)
        {
            throw new InvalidOperationException("The tenant cannot change within a scope.");
        }

        TenantId = tenantId;
    }
}
