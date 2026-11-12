using OpenIddict.EntityFrameworkCore.Models;

namespace Identity.Infrastructure.Persistence.Entities;

/// <summary>Required by OpenIddict; stays empty because scopes are registered in code.</summary>
public sealed class PlatformScope : OpenIddictEntityFrameworkCoreScope<Guid>;
