namespace Identity.Infrastructure.Seeding;

public sealed class SeedOptions
{
    public SeedTenantOptions TenantA { get; set; } = new();

    public SeedTenantOptions TenantB { get; set; } = new();

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(TenantA.UserPassword))
        {
            throw new InvalidOperationException("Missing required setting 'Seed:TenantA:UserPassword'.");
        }

        if (string.IsNullOrWhiteSpace(TenantB.UserPassword))
        {
            throw new InvalidOperationException("Missing required setting 'Seed:TenantB:UserPassword'.");
        }
    }
}
