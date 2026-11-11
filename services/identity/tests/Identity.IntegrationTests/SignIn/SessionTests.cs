using System.Net;
using Identity.Infrastructure.Seeding;
using Identity.IntegrationTests.Infrastructure;
using Microsoft.Extensions.Time.Testing;

namespace Identity.IntegrationTests.SignIn;

[Collection(PostgresCollection.Name)]
public sealed class SessionTests(PostgresFixture database)
{
    [Fact]
    public async Task Authorize_WhenSessionInSameTenant_ShouldNotAskForCredentials()
    {
        // Arrange
        await using var factory = new IdentityApiFactory(database.AppConnectionString);
        var client = new OidcTestClient(factory.CreateBrowser());
        await client.SignInForCodeAsync(SeedData.ClientAId, SeedData.Username, IdentityApiFactory.TenantAPassword);

        // Act
        var response = await client.StartAsync(SeedData.ClientAId);

        // Assert
        Assert.False(string.IsNullOrEmpty(OidcTestClient.CodeFrom(response)));
    }

    [Fact]
    public async Task Authorize_WhenSessionInAnotherTenant_ShouldAskForCredentials()
    {
        // Arrange
        await using var factory = new IdentityApiFactory(database.AppConnectionString);
        var client = new OidcTestClient(factory.CreateBrowser());
        await client.SignInForCodeAsync(SeedData.ClientAId, SeedData.Username, IdentityApiFactory.TenantAPassword);

        // Act
        var response = await client.StartAsync(SeedData.ClientBId);

        // Assert
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = OidcTestClient.LocationOf(response);
        Assert.StartsWith("/account/login", location);
        Assert.DoesNotContain("code=", location.Split("code_challenge=")[0]);
    }

    [Fact]
    public async Task Authorize_WhenSessionOlderThanDefault_ShouldAskForCredentials()
    {
        // Arrange
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = new IdentityApiFactory(database.AppConnectionString, timeProvider: time);
        var client = new OidcTestClient(factory.CreateBrowser());
        await client.SignInForCodeAsync(SeedData.ClientAId, SeedData.Username, IdentityApiFactory.TenantAPassword);

        // Act
        time.Advance(TimeSpan.FromMinutes(14));
        var stillValid = await client.StartAsync(SeedData.ClientAId);
        time.Advance(TimeSpan.FromMinutes(2));
        var expired = await client.StartAsync(SeedData.ClientAId);

        // Assert
        Assert.False(string.IsNullOrEmpty(OidcTestClient.CodeFrom(stillValid)));
        Assert.StartsWith("/account/login", OidcTestClient.LocationOf(expired));
    }

    [Fact]
    public async Task Authorize_WhenSessionOlderThanConfiguredDuration_ShouldAskForCredentials()
    {
        // Arrange
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var settings = new Dictionary<string, string?>(IdentityApiFactory.DefaultSeedSettings)
        {
            ["Identity:Session:Duration"] = "00:02:00",
        };
        await using var factory = new IdentityApiFactory(database.AppConnectionString, settings: settings, timeProvider: time);
        var client = new OidcTestClient(factory.CreateBrowser());
        await client.SignInForCodeAsync(SeedData.ClientAId, SeedData.Username, IdentityApiFactory.TenantAPassword);

        // Act
        time.Advance(TimeSpan.FromMinutes(3));
        var response = await client.StartAsync(SeedData.ClientAId);

        // Assert
        Assert.StartsWith("/account/login", OidcTestClient.LocationOf(response));
    }
}
