using Identity.Domain.Tenancy;
using Identity.Domain.Users;
using Identity.Infrastructure.Persistence.Interceptors;

namespace Identity.UnitTests.Tenancy;

public sealed class TenantWriteGuardTests
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();

    [Fact]
    public void Apply_WhenTenantMissing_ShouldThrow()
    {
        // Arrange
        ITenantOwned entity = LocalUser.Create("user", "hash");

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => TenantWriteGuard.Apply(entity, tenantId: null));
    }

    [Fact]
    public void Apply_WhenTenantDiffers_ShouldThrow()
    {
        // Arrange
        ITenantOwned entity = LocalUser.Create("user", "hash");
        entity.TenantId = TenantB;

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => TenantWriteGuard.Apply(entity, TenantA));
    }

    [Fact]
    public void Apply_WhenEntityHasNoTenant_ShouldStampCurrentTenant()
    {
        // Arrange
        ITenantOwned entity = LocalUser.Create("user", "hash");

        // Act
        TenantWriteGuard.Apply(entity, TenantA);

        // Assert
        Assert.Equal(TenantA, entity.TenantId);
    }
}
