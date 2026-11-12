using System.Net;
using System.Text.Json;
using Identity.Infrastructure.Seeding;
using Identity.IntegrationTests.Infrastructure;

namespace Identity.IntegrationTests.SignIn;

/// <summary>OIDC ties the username to the profile scope, so a client gets it only when it asks for it.</summary>
[Collection(PostgresCollection.Name)]
public sealed class ClaimReleaseTests(PostgresFixture database)
{
    [Fact]
    public async Task SignIn_WhenProfileScopeNotRequested_ShouldNotReleaseUsername()
    {
        // Arrange
        await using var factory = new IdentityApiFactory(database.AppConnectionString);
        var client = new OidcTestClient(factory.CreateBrowser()) { Scope = "openid" };

        // Act
        var (idToken, accessToken, userInfo) = await SignInAsync(client);

        // Assert
        Assert.False(idToken.TryGetProperty("preferred_username", out _));
        Assert.False(accessToken.TryGetProperty("preferred_username", out _));
        Assert.False(userInfo.TryGetProperty("preferred_username", out _));
        Assert.Equal(SeedData.TenantAId.ToString(), userInfo.GetProperty("tenant_id").GetString());
    }

    [Fact]
    public async Task SignIn_WhenProfileScopeRequested_ShouldReleaseUsername()
    {
        // Arrange
        await using var factory = new IdentityApiFactory(database.AppConnectionString);
        var client = new OidcTestClient(factory.CreateBrowser()) { Scope = "openid profile" };

        // Act
        var (idToken, accessToken, userInfo) = await SignInAsync(client);

        // Assert
        Assert.Equal(SeedData.Username, idToken.GetProperty("preferred_username").GetString());
        Assert.Equal(SeedData.Username, accessToken.GetProperty("preferred_username").GetString());
        Assert.Equal(SeedData.Username, userInfo.GetProperty("preferred_username").GetString());
    }

    private static async Task<(JsonElement IdToken, JsonElement AccessToken, JsonElement UserInfo)> SignInAsync(OidcTestClient client)
    {
        var code = await client.SignInForCodeAsync(SeedData.ClientAId, SeedData.Username, IdentityApiFactory.TenantAPassword);
        var exchange = await client.ExchangeAsync(SeedData.ClientAId, code);
        Assert.Equal(HttpStatusCode.OK, exchange.StatusCode);
        using var tokens = JsonDocument.Parse(await exchange.Content.ReadAsStringAsync());
        var accessToken = tokens.RootElement.GetProperty("access_token").GetString()!;
        var idToken = tokens.RootElement.GetProperty("id_token").GetString()!;

        var userInfo = await client.UserInfoAsync(accessToken);
        Assert.Equal(HttpStatusCode.OK, userInfo.StatusCode);
        using var userInfoJson = JsonDocument.Parse(await userInfo.Content.ReadAsStringAsync());

        return (OidcTestClient.ReadJwtPayload(idToken), OidcTestClient.ReadJwtPayload(accessToken), userInfoJson.RootElement.Clone());
    }
}
