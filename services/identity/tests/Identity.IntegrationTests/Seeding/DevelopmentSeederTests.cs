using System.Security.Cryptography.X509Certificates;
using Identity.Infrastructure.Seeding;
using Identity.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Identity.IntegrationTests.Seeding;

[Collection(PostgresCollection.Name)]
public sealed class DevelopmentSeederTests(PostgresFixture database)
{
    [Fact]
    public async Task Seed_WhenEmpty_ShouldCreateTwoTenantsWithClientsAndUsers()
    {
        // Arrange
        await using var factory = new IdentityApiFactory(database.AppConnectionString);

        // Act
        _ = factory.CreateBrowser();

        // Assert
        await using var tenantA = TestDb.CreateContext(database.AppConnectionString, SeedData.TenantAId);
        await using var tenantB = TestDb.CreateContext(database.AppConnectionString, SeedData.TenantBId);

        Assert.Equal(2, await tenantA.ClientDirectory.CountAsync());
        Assert.Equal(SeedData.ClientAId, (await tenantA.Applications.SingleAsync()).ClientId);
        Assert.Equal(SeedData.ClientBId, (await tenantB.Applications.SingleAsync()).ClientId);

        var userA = await tenantA.LocalUsers.SingleAsync(user => user.Username == SeedData.Username);
        var userB = await tenantB.LocalUsers.SingleAsync(user => user.Username == SeedData.Username);
        Assert.NotEqual(userA.Id, userB.Id);
        Assert.NotEqual(userA.PasswordHash, userB.PasswordHash);
    }

    [Fact]
    public async Task Seed_WhenRunTwice_ShouldNotDuplicateOrChangeData()
    {
        // Arrange
        await using (var first = new IdentityApiFactory(database.AppConnectionString))
        {
            _ = first.CreateBrowser();
        }

        await using var context = TestDb.CreateContext(database.AppConnectionString, SeedData.TenantAId);
        var hashBefore = (await context.LocalUsers.AsNoTracking().SingleAsync(user => user.Username == SeedData.Username)).PasswordHash;

        // Act
        await using var second = new IdentityApiFactory(database.AppConnectionString);
        _ = second.CreateBrowser();

        // Assert
        var usersAfter = await context.LocalUsers.AsNoTracking().Where(user => user.Username == SeedData.Username).ToListAsync();
        Assert.Equal(hashBefore, Assert.Single(usersAfter).PasswordHash);
        Assert.Equal(1, await context.Applications.CountAsync());
        Assert.Equal(2, await context.ClientDirectory.CountAsync());
    }

    [Fact]
    public async Task Startup_WhenSeedSecretMissing_ShouldFailAndNameSettingOnly()
    {
        // Arrange
        const string otherSecret = "visible-only-if-leaked";
        await using var factory = new IdentityApiFactory(
            database.AppConnectionString,
            settings: new Dictionary<string, string?> { ["Seed:TenantB:UserPassword"] = otherSecret });

        // Act
        var error = Assert.ThrowsAny<Exception>(() => factory.CreateBrowser());

        // Assert
        Assert.Contains("Seed:TenantA:UserPassword", error.ToString());
        Assert.DoesNotContain(otherSecret, error.ToString());
    }

    [Fact]
    public async Task Startup_WhenProduction_ShouldNotSeed()
    {
        // Arrange: an empty database of its own, since other tests seed the shared one.
        var ownDatabase = new PostgresFixture();
        await ownDatabase.InitializeAsync();
        await using var cleanup = ownDatabase;

        var settings = new Dictionary<string, string?>(IdentityApiFactory.DefaultSeedSettings)
        {
            ["Identity:Signing:CertificateBase64"] = TestCertificates.CreateBase64(X509KeyUsageFlags.DigitalSignature),
            ["Identity:Encryption:CertificateBase64"] = TestCertificates.CreateBase64(X509KeyUsageFlags.KeyEncipherment),
        };
        await using var factory = new IdentityApiFactory(ownDatabase.AppConnectionString, "Production", settings);

        // Act
        _ = factory.CreateBrowser();

        // Assert
        await using var context = TestDb.CreateContext(ownDatabase.AppConnectionString, SeedData.TenantAId);
        Assert.Equal(0, await context.ClientDirectory.CountAsync());
        Assert.Equal(0, await context.LocalUsers.CountAsync());
        Assert.Equal(0, await context.Applications.CountAsync());
    }
}
