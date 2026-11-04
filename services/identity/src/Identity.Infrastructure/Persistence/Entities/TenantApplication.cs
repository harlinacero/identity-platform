using Identity.Domain.Tenancy;
using OpenIddict.EntityFrameworkCore.Models;

namespace Identity.Infrastructure.Persistence.Entities;

public sealed class TenantApplication : OpenIddictEntityFrameworkCoreApplication<Guid, TenantAuthorization, TenantToken>, ITenantOwned
{
    public Guid TenantId { get; set; }
}
