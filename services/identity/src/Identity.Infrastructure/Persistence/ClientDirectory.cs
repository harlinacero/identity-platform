using Identity.Application.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence;

public sealed class ClientDirectory(IdentityDbContext context) : IClientDirectory
{
    public Task<Guid?> FindTenantIdAsync(string clientId, CancellationToken cancellationToken) =>
        context.ClientDirectory
            .Where(entry => entry.ClientId == clientId)
            .Select(entry => (Guid?)entry.TenantId)
            .SingleOrDefaultAsync(cancellationToken);
}
