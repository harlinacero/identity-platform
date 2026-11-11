using System.Net;
using Identity.Infrastructure.Seeding;
using Identity.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;

namespace Identity.IntegrationTests.Resilience;

/// <summary>Each test owns its database, because it stops and restarts it.</summary>
public sealed class DataStoreOutageTests
{
    [Fact]
    public async Task Login_WhenDatabaseUnavailable_ShouldReturnTemporaryErrorAndCreateNothing()
    {
        // Arrange
        var database = new PostgresFixture();
        await database.InitializeAsync();
        await using var cleanup = database;
        await using var factory = new IdentityApiFactory(database.AppConnectionString);
        var browser = factory.CreateBrowser();
        var client = new OidcTestClient(browser);
        var loginUrl = OidcTestClient.LocationOf(await client.StartAsync(SeedData.ClientAId));
        var form = await client.ReadLoginFormAsync(loginUrl);

        // Act
        await database.StopDatabaseAsync();
        var response = await client.PostLoginAsync(form, SeedData.Username, IdentityApiFactory.TenantAPassword);

        // Assert
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.False(
            response.Headers.TryGetValues("Set-Cookie", out var cookies)
            && cookies.Any(cookie => cookie.StartsWith("identity.session")));
    }

    [Fact]
    public async Task Exchange_WhenDatabaseUnavailable_ShouldReturnTemporaryError()
    {
        // Arrange
        var database = new PostgresFixture();
        await database.InitializeAsync();
        await using var cleanup = database;
        await using var factory = new IdentityApiFactory(database.AppConnectionString);
        var client = new OidcTestClient(factory.CreateBrowser());
        var code = await client.SignInForCodeAsync(SeedData.ClientAId, SeedData.Username, IdentityApiFactory.TenantAPassword);

        // Act
        await database.StopDatabaseAsync();
        var response = await client.ExchangeAsync(SeedData.ClientAId, code);
        var health = await factory.CreateBrowser().GetAsync("/health");
        await database.StartDatabaseAsync();

        // Assert
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.DoesNotContain("access_token", await response.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.ServiceUnavailable, health.StatusCode);

        await using var context = TestDb.CreateContext(database.AppConnectionString, SeedData.TenantAId);
        Assert.Equal(0, await context.Tokens.CountAsync(token => token.Type == OpenIddictConstants.TokenTypeIdentifiers.AccessToken));
    }

    [Fact]
    public async Task Health_WhenDatabaseAvailable_ShouldBeHealthy()
    {
        // Arrange
        var database = new PostgresFixture();
        await database.InitializeAsync();
        await using var cleanup = database;
        await using var factory = new IdentityApiFactory(database.AppConnectionString);

        // Act
        var response = await factory.CreateBrowser().GetAsync("/health");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
