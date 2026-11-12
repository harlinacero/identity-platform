namespace Identity.Domain.Tenancy;

public interface ITenantOwned
{
    Guid TenantId { get; set; }
}
