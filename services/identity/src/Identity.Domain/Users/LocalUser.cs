using Identity.Domain.Tenancy;

namespace Identity.Domain.Users;

/// <summary>Temporary in Identity; local users move to Tenants &amp; Users in Phase 2.</summary>
public sealed class LocalUser : ITenantOwned
{
    private LocalUser()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; set; }

    public string Username { get; private set; } = string.Empty;

    public string NormalizedUsername { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public static LocalUser Create(string username, string passwordHash) => new()
    {
        Id = Guid.CreateVersion7(),
        Username = username,
        NormalizedUsername = Normalize(username),
        PasswordHash = passwordHash,
        CreatedAt = DateTimeOffset.UtcNow,
    };

    public static string Normalize(string username) => username.Trim().ToUpperInvariant();
}
