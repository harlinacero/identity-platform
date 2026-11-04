using Identity.Application.Tenancy;

namespace Identity.UnitTests.Tenancy;

public sealed class TenantContextTests
{
    [Fact]
    public void Set_WhenChangedWithinScope_ShouldThrow()
    {
        // Arrange
        var context = new TenantContext();
        context.Set(Guid.NewGuid());

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => context.Set(Guid.NewGuid()));
    }

    [Fact]
    public void Set_WhenSameTenantSetAgain_ShouldKeepIt()
    {
        // Arrange
        var context = new TenantContext();
        var tenant = Guid.NewGuid();
        context.Set(tenant);

        // Act
        context.Set(tenant);

        // Assert
        Assert.Equal(tenant, context.TenantId);
    }
}
