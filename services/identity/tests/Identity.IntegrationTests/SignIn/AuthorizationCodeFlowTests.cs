using System.Net;
using System.Text.Json;
using Identity.Infrastructure.Seeding;
using Identity.IntegrationTests.Infrastructure;

namespace Identity.IntegrationTests.SignIn;

[Collection(PostgresCollection.Name)]
public sealed class AuthorizationCodeFlowTests(PostgresFixture database)
{
    [Fact]
    public async Task CodeFlow_WhenCredentialsValid_ShouldIssueTokenWithUserAndTenant()
    {
        // Arrange
        await using var factory = new IdentityApiFactory(database.AppConnectionString);
        var client = new OidcTestClient(factory.CreateBrowser());

        // Act
        var code = await client.SignInForCodeAsync(SeedData.ClientAId, SeedData.Username, IdentityApiFactory.TenantAPassword);
        var response = await client.ExchangeAsync(SeedData.ClientAId, code);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var payload = OidcTestClient.ReadJwtPayload(json.RootElement.GetProperty("access_token").GetString()!);

        Assert.Equal(SeedData.TenantAId.ToString(), payload.GetProperty("tenant_id").GetString());
        Assert.False(string.IsNullOrEmpty(payload.GetProperty("sub").GetString()));
        Assert.Equal(900, payload.GetProperty("exp").GetInt64() - payload.GetProperty("iat").GetInt64());
    }
}
