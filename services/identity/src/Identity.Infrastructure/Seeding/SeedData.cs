namespace Identity.Infrastructure.Seeding;

/// <summary>Fixed, non-secret test data for local and test environments.</summary>
public static class SeedData
{
    public const string ClientAId = "tenant-a-web";
    public const string ClientBId = "tenant-b-web";
    public const string Username = "demo.user";
    public const string RedirectUri = "http://localhost:5173/callback";

    public static readonly Guid TenantAId = Guid.Parse("0a000000-0000-4000-8000-00000000000a");
    public static readonly Guid TenantBId = Guid.Parse("0b000000-0000-4000-8000-00000000000b");
}
