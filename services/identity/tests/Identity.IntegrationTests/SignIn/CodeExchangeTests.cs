using System.Net;
using Identity.Infrastructure.Seeding;
using Identity.IntegrationTests.Infrastructure;
using Microsoft.Extensions.Time.Testing;

namespace Identity.IntegrationTests.SignIn;

[Collection(PostgresCollection.Name)]
public sealed class CodeExchangeTests(PostgresFixture database)
{
    [Fact]
    public async Task Exchange_WhenVerifierDoesNotMatch_ShouldIssueNoToken()
    {
        // Arrange
        await using var factory = new IdentityApiFactory(database.AppConnectionString);
        var client = new OidcTestClient(factory.CreateBrowser());
        var code = await client.SignInForCodeAsync(SeedData.ClientAId, SeedData.Username, IdentityApiFactory.TenantAPassword);

        // Act
        var response = await client.ExchangeAsync(SeedData.ClientAId, code, verifier: new OidcTestClient(factory.CreateBrowser()).Verifier);

        // Assert
        await AssertInvalidGrantAsync(response);
    }

    [Fact]
    public async Task Exchange_WhenCodeWithinFiveMinutes_ShouldSucceed()
    {
        // Arrange
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = new IdentityApiFactory(database.AppConnectionString, timeProvider: time);
        var client = new OidcTestClient(factory.CreateBrowser());
        var code = await client.SignInForCodeAsync(SeedData.ClientAId, SeedData.Username, IdentityApiFactory.TenantAPassword);

        // Act
        time.Advance(TimeSpan.FromMinutes(4));
        var response = await client.ExchangeAsync(SeedData.ClientAId, code);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Exchange_WhenCodeOlderThanFiveMinutes_ShouldBeRejected()
    {
        // Arrange
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = new IdentityApiFactory(database.AppConnectionString, timeProvider: time);
        var client = new OidcTestClient(factory.CreateBrowser());
        var code = await client.SignInForCodeAsync(SeedData.ClientAId, SeedData.Username, IdentityApiFactory.TenantAPassword);

        // Act
        time.Advance(TimeSpan.FromMinutes(6));
        var response = await client.ExchangeAsync(SeedData.ClientAId, code);

        // Assert
        await AssertInvalidGrantAsync(response);
    }

    [Fact]
    public async Task Exchange_WhenCodeAlreadyUsed_ShouldBeRejected()
    {
        // Arrange
        await using var factory = new IdentityApiFactory(database.AppConnectionString);
        var client = new OidcTestClient(factory.CreateBrowser());
        var code = await client.SignInForCodeAsync(SeedData.ClientAId, SeedData.Username, IdentityApiFactory.TenantAPassword);
        var first = await client.ExchangeAsync(SeedData.ClientAId, code);

        // Act
        var second = await client.ExchangeAsync(SeedData.ClientAId, code);

        // Assert
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        await AssertInvalidGrantAsync(second);
    }

    [Fact]
    public async Task Exchange_WhenPresentedByOtherTenantClient_ShouldBeRejected()
    {
        // Arrange
        await using var factory = new IdentityApiFactory(database.AppConnectionString);
        var client = new OidcTestClient(factory.CreateBrowser());
        var code = await client.SignInForCodeAsync(SeedData.ClientAId, SeedData.Username, IdentityApiFactory.TenantAPassword);

        // Act
        var response = await client.ExchangeAsync(SeedData.ClientBId, code);

        // Assert
        await AssertInvalidGrantAsync(response);
    }

    private static async Task AssertInvalidGrantAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("invalid_grant", body);
        Assert.DoesNotContain("access_token", body);
    }
}
