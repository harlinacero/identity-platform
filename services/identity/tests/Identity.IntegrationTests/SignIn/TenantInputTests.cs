using System.Net;
using Identity.Infrastructure.Seeding;
using Identity.IntegrationTests.Infrastructure;

namespace Identity.IntegrationTests.SignIn;

[Collection(PostgresCollection.Name)]
public sealed class TenantInputTests(PostgresFixture database)
{
    [Fact]
    public async Task SignIn_WhenRequestStatesAnotherTenant_ShouldUseClientTenant()
    {
        // Arrange
        var statedTenant = $"tenant_id={SeedData.TenantBId}&tenant={SeedData.TenantBId}";
        await using var factory = new IdentityApiFactory(database.AppConnectionString);
        var browser = factory.CreateBrowser();
        var client = new OidcTestClient(browser);
        var start = await client.StartAsync(SeedData.ClientAId, statedTenant);
        var loginUrl = OidcTestClient.LocationOf(start);

        // Act
        var withTenantBPassword = await client.SubmitCredentialsAsync(loginUrl, SeedData.Username, IdentityApiFactory.TenantBPassword);
        var withTenantAPassword = await client.SubmitCredentialsAsync(loginUrl, SeedData.Username, IdentityApiFactory.TenantAPassword);
        var authorize = await browser.GetAsync(OidcTestClient.LocationOf(withTenantAPassword));
        var exchange = await client.ExchangeAsync(SeedData.ClientAId, OidcTestClient.CodeFrom(authorize));

        // Assert
        Assert.Equal(HttpStatusCode.OK, withTenantBPassword.StatusCode);
        Assert.Contains("not valid", await withTenantBPassword.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, exchange.StatusCode);
        var body = System.Text.Json.JsonDocument.Parse(await exchange.Content.ReadAsStringAsync());
        var payload = OidcTestClient.ReadJwtPayload(body.RootElement.GetProperty("access_token").GetString()!);
        Assert.Equal(SeedData.TenantAId.ToString(), payload.GetProperty("tenant_id").GetString());
    }
}
