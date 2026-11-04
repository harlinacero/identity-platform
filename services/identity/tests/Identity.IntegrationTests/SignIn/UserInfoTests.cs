using System.Net;
using System.Text.Json;
using Identity.Infrastructure.Seeding;
using Identity.IntegrationTests.Infrastructure;

namespace Identity.IntegrationTests.SignIn;

[Collection(PostgresCollection.Name)]
public sealed class UserInfoTests(PostgresFixture database)
{
    [Fact]
    public async Task UserInfo_WhenTokenValid_ShouldReturnUserAndTenant()
    {
        // Arrange
        await using var factory = new IdentityApiFactory(database.AppConnectionString);
        var client = new OidcTestClient(factory.CreateBrowser());
        var accessToken = await client.SignInForAccessTokenAsync(SeedData.ClientBId, SeedData.Username, IdentityApiFactory.TenantBPassword);

        // Act
        var response = await client.UserInfoAsync(accessToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(SeedData.TenantBId.ToString(), json.RootElement.GetProperty("tenant_id").GetString());
        Assert.Equal(SeedData.Username, json.RootElement.GetProperty("preferred_username").GetString());
        Assert.False(string.IsNullOrEmpty(json.RootElement.GetProperty("sub").GetString()));
    }
}
