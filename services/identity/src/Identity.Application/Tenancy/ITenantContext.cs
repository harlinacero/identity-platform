namespace Identity.Application.Tenancy;

public interface ITenantContext
{
    Guid? TenantId { get; }
}
