using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Seeding;
using Identity.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Identity.IntegrationTests.Isolation;

/// <summary>The proof required by ADR-0006: RLS works with OpenIddict's own records.</summary>
[Collection(PostgresCollection.Name)]
public sealed class SignInRecordIsolationTests(PostgresFixture database)
{
    [Fact]
    public async Task SignIn_WhenRlsEnforced_ShouldSucceedForBothTenantsAndHideEachOthersRecords()
    {
        // Arrange
        await using var factory = new IdentityApiFactory(database.AppConnectionString);

        // Act: complete sign-ins, using every record type, through the runtime role.
        var clientA = new OidcTestClient(factory.CreateBrowser());
        var clientB = new OidcTestClient(factory.CreateBrowser());
        var tokenA = await clientA.SignInForAccessTokenAsync(SeedData.ClientAId, SeedData.Username, IdentityApiFactory.TenantAPassword);
        var tokenB = await clientB.SignInForAccessTokenAsync(SeedData.ClientBId, SeedData.Username, IdentityApiFactory.TenantBPassword);

        // Assert: reading a token entry also goes through RLS.
        Assert.True((await clientA.UserInfoAsync(tokenA)).IsSuccessStatusCode);
        Assert.True((await clientB.UserInfoAsync(tokenB)).IsSuccessStatusCode);

        await AssertOnlyOwnRecordsAsync(SeedData.TenantAId);
        await AssertOnlyOwnRecordsAsync(SeedData.TenantBId);
        await AssertNothingVisibleWithoutTenantAsync();
    }

    private async Task AssertOnlyOwnRecordsAsync(Guid tenantId)
    {
        await using var context = TestDb.CreateContext(database.AppConnectionString, tenantId);

        var applications = await context.Applications.IgnoreQueryFilters().ToListAsync();
        var authorizations = await context.Authorizations.IgnoreQueryFilters().ToListAsync();
        var tokens = await context.Tokens.IgnoreQueryFilters().ToListAsync();

        Assert.NotEmpty(applications);
        Assert.NotEmpty(authorizations);
        Assert.NotEmpty(tokens);
        Assert.All(applications, record => Assert.Equal(tenantId, record.TenantId));
        Assert.All(authorizations, record => Assert.Equal(tenantId, record.TenantId));
        Assert.All(tokens, record => Assert.Equal(tenantId, record.TenantId));
    }

    private async Task AssertNothingVisibleWithoutTenantAsync()
    {
        await using IdentityDbContext context = TestDb.CreateContext(database.AppConnectionString, tenantId: null);

        Assert.Empty(await context.Applications.IgnoreQueryFilters().ToListAsync());
        Assert.Empty(await context.Authorizations.IgnoreQueryFilters().ToListAsync());
        Assert.Empty(await context.Tokens.IgnoreQueryFilters().ToListAsync());
    }
}
