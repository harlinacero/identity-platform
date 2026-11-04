namespace Identity.Infrastructure.Persistence;

/// <summary>
/// Maps a public client id to its tenant. Has no RLS, because the tenant must be known
/// before tenant-protected data can be read, and it holds only public values (ADR-0006).
/// </summary>
public sealed class ClientDirectoryEntry
{
    public required string ClientId { get; init; }

    public required Guid TenantId { get; init; }
}
