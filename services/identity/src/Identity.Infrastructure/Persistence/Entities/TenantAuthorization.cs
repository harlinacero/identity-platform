using Identity.Domain.Tenancy;
using OpenIddict.EntityFrameworkCore.Models;

namespace Identity.Infrastructure.Persistence.Entities;

public sealed class TenantAuthorization : OpenIddictEntityFrameworkCoreAuthorization<Guid, TenantApplication, TenantToken>, ITenantOwned
{
    public Guid TenantId { get; set; }
}
