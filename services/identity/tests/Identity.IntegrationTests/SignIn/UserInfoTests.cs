using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Identity.Infrastructure.Seeding;
using Identity.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using OpenIddict.Server;

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

    [Fact]
    public async Task UserInfo_WhenTokenHasNoTenant_ShouldBeUnauthorized()
    {
        // Arrange
        await using var factory = new IdentityApiFactory(database.AppConnectionString);
        var client = new OidcTestClient(factory.CreateBrowser());
        var accessToken = await client.SignInForAccessTokenAsync(SeedData.ClientAId, SeedData.Username, IdentityApiFactory.TenantAPassword);

        // Act: re-signing with the server's key changes only what the test removes.
        var resignedIntact = await client.UserInfoAsync(Resign(factory, accessToken, _ => { }));
        var withoutTenant = await client.UserInfoAsync(Resign(factory, accessToken, payload => payload.Remove("tenant_id")));

        // Assert
        Assert.Equal(HttpStatusCode.OK, resignedIntact.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, withoutTenant.StatusCode);
    }

    [Fact]
    public async Task UserInfo_WhenSignedTokenNamesAnotherTenant_ShouldBeUnauthorized()
    {
        // Arrange: a validly signed token whose tenant claim no longer matches its stored entry.
        await using var factory = new IdentityApiFactory(database.AppConnectionString);
        var client = new OidcTestClient(factory.CreateBrowser());
        var accessToken = await client.SignInForAccessTokenAsync(SeedData.ClientAId, SeedData.Username, IdentityApiFactory.TenantAPassword);
        var otherTenant = Resign(factory, accessToken, payload => payload["tenant_id"] = SeedData.TenantBId.ToString());

        // Act
        var response = await client.UserInfoAsync(otherTenant);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UserInfo_WhenTokenExpired_ShouldBeUnauthorized()
    {
        // Arrange
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using var factory = new IdentityApiFactory(database.AppConnectionString, timeProvider: time);
        var client = new OidcTestClient(factory.CreateBrowser());
        var accessToken = await client.SignInForAccessTokenAsync(SeedData.ClientAId, SeedData.Username, IdentityApiFactory.TenantAPassword);

        // Act
        time.Advance(TimeSpan.FromMinutes(16));
        var response = await client.UserInfoAsync(accessToken);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UserInfo_WhenTokenAltered_ShouldBeUnauthorized()
    {
        // Arrange
        await using var factory = new IdentityApiFactory(database.AppConnectionString);
        var client = new OidcTestClient(factory.CreateBrowser());
        var accessToken = await client.SignInForAccessTokenAsync(SeedData.ClientAId, SeedData.Username, IdentityApiFactory.TenantAPassword);
        var parts = accessToken.Split('.');
        var payload = JsonNode.Parse(WebEncoders.Base64UrlDecode(parts[1]))!.AsObject();
        payload["tenant_id"] = SeedData.TenantBId.ToString();
        var altered = $"{parts[0]}.{WebEncoders.Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(payload))}.{parts[2]}";

        // Act
        var response = await client.UserInfoAsync(altered);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UserInfo_WhenNoToken_ShouldBeUnauthorized()
    {
        // Arrange
        await using var factory = new IdentityApiFactory(database.AppConnectionString);
        var client = new OidcTestClient(factory.CreateBrowser());

        // Act
        var response = await client.UserInfoAsync(accessToken: null);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static string Resign(IdentityApiFactory factory, string accessToken, Action<JsonObject> change)
    {
        var options = factory.Services.GetRequiredService<IOptionsMonitor<OpenIddictServerOptions>>().CurrentValue;
        var payload = JsonNode.Parse(WebEncoders.Base64UrlDecode(accessToken.Split('.')[1]))!.AsObject();
        change(payload);

        return new JsonWebTokenHandler().CreateToken(
            payload.ToJsonString(),
            options.SigningCredentials[0],
            new Dictionary<string, object> { ["typ"] = "at+jwt" });
    }
}
