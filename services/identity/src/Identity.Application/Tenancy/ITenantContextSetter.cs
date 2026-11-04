namespace Identity.Application.Tenancy;

public interface ITenantContextSetter
{
    /// <summary>Establishes the tenant for this scope. Changing it to another tenant throws.</summary>
    void Set(Guid tenantId);
}
