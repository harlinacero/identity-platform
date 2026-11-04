using Identity.Domain.Tenancy;
using OpenIddict.EntityFrameworkCore.Models;

namespace Identity.Infrastructure.Persistence.Entities;

public sealed class TenantToken : OpenIddictEntityFrameworkCoreToken<Guid, TenantApplication, TenantAuthorization>, ITenantOwned
{
    public Guid TenantId { get; set; }
}
