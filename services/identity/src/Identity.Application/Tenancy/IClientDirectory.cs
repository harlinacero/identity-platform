namespace Identity.Application.Tenancy;

public interface IClientDirectory
{
    Task<Guid?> FindTenantIdAsync(string clientId, CancellationToken cancellationToken);
}
