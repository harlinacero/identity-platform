using System.Net;
using System.Text.Json;
using Identity.Infrastructure.Seeding;
using Identity.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.WebUtilities;

namespace Identity.IntegrationTests.SignIn;

[Collection(PostgresCollection.Name)]
public sealed class AuthorizationRequestTests(PostgresFixture database)
{
    [Fact]
    public async Task Authorize_WhenClientUnknown_ShouldRejectWithoutRedirect()
    {
        // Arrange
        await using var factory = new IdentityApiFactory(database.AppConnectionString);
        var client = new OidcTestClient(factory.CreateBrowser());

        // Act
        var response = await client.StartAsync("unknown-client");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Theory]
    [InlineData("http://localhost:5173/other")]
    [InlineData("http://localhost:5173/callback?extra=1")]
    [InlineData("http://localhost:5173/CALLBACK")]
    [InlineData("http://localhost:5174/callback")]
    [InlineData("https://attacker.example/callback")]
    public async Task Authorize_WhenRedirectUriDiffers_ShouldRejectWithoutRedirect(string redirectUri)
    {
        // Arrange
        await using var factory = new IdentityApiFactory(database.AppConnectionString);
        var browser = factory.CreateBrowser();
        var client = new OidcTestClient(browser);
        var url = client.AuthorizeUrl(SeedData.ClientAId)
            .Replace(Uri.EscapeDataString(SeedData.RedirectUri), Uri.EscapeDataString(redirectUri));

        // Act
        var response = await browser.GetAsync(url);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task Authorize_WhenChallengeMissing_ShouldReject()
    {
        // Arrange
        await using var factory = new IdentityApiFactory(database.AppConnectionString);
        var browser = factory.CreateBrowser();
        var url = $"/connect/authorize?client_id={SeedData.ClientAId}&response_type=code&scope=openid" +
            $"&redirect_uri={Uri.EscapeDataString(SeedData.RedirectUri)}&state=test-state";

        // Act
        var response = await browser.GetAsync(url);

        // Assert
        await AssertRejectedAsInvalidAsync(response);
    }

    [Fact]
    public async Task Authorize_WhenChallengeMethodPlain_ShouldReject()
    {
        // Arrange
        await using var factory = new IdentityApiFactory(database.AppConnectionString);
        var browser = factory.CreateBrowser();
        var client = new OidcTestClient(browser);
        var url = client.AuthorizeUrl(SeedData.ClientAId).Replace("code_challenge_method=S256", "code_challenge_method=plain");

        // Act
        var response = await browser.GetAsync(url);

        // Assert
        await AssertRejectedAsInvalidAsync(response);
    }

    [Fact]
    public async Task Authorize_WhenIdTokenHintFromAnotherTenant_ShouldNotAffectTenant()
    {
        // Arrange: an ID token from tenant A, and a browser signed in to tenant B.
        await using var factory = new IdentityApiFactory(database.AppConnectionString);
        var tenantAClient = new OidcTestClient(factory.CreateBrowser());
        var tenantACode = await tenantAClient.SignInForCodeAsync(SeedData.ClientAId, SeedData.Username, IdentityApiFactory.TenantAPassword);
        var exchange = await tenantAClient.ExchangeAsync(SeedData.ClientAId, tenantACode);
        using var tokens = JsonDocument.Parse(await exchange.Content.ReadAsStringAsync());
        var tenantAIdToken = tokens.RootElement.GetProperty("id_token").GetString()!;

        var browser = factory.CreateBrowser();
        var tenantBClient = new OidcTestClient(browser);
        await tenantBClient.SignInForCodeAsync(SeedData.ClientBId, SeedData.Username, IdentityApiFactory.TenantBPassword);

        // Act
        var response = await tenantBClient.StartAsync(SeedData.ClientBId, $"id_token_hint={tenantAIdToken}");

        // Assert
        // OpenIddict discards a hint that fails validation, so the request stays entirely in tenant B.
        var accessToken = await ReadAccessTokenAsync(await tenantBClient.ExchangeAsync(SeedData.ClientBId, OidcTestClient.CodeFrom(response)));
        Assert.Equal(SeedData.TenantBId.ToString(), OidcTestClient.ReadJwtPayload(accessToken).GetProperty("tenant_id").GetString());
    }

    [Fact]
    public async Task Authorize_WhenRequestIsPost_ShouldCompleteSignIn()
    {
        // Arrange
        await using var factory = new IdentityApiFactory(database.AppConnectionString);
        var browser = factory.CreateBrowser();
        var client = new OidcTestClient(browser);
        var parameters = QueryHelpers.ParseQuery(new Uri(new Uri("http://localhost"), client.AuthorizeUrl(SeedData.ClientAId)).Query)
            .ToDictionary(parameter => parameter.Key, parameter => parameter.Value.ToString());

        // Act
        var start = await browser.PostAsync("/connect/authorize", new FormUrlEncodedContent(parameters));
        var login = await client.SubmitCredentialsAsync(OidcTestClient.LocationOf(start), SeedData.Username, IdentityApiFactory.TenantAPassword);
        var authorize = await browser.GetAsync(OidcTestClient.LocationOf(login));
        var exchange = await client.ExchangeAsync(SeedData.ClientAId, OidcTestClient.CodeFrom(authorize));

        // Assert
        Assert.Equal(HttpStatusCode.OK, exchange.StatusCode);
    }

    private static async Task<string> ReadAccessTokenAsync(HttpResponseMessage exchange)
    {
        Assert.Equal(HttpStatusCode.OK, exchange.StatusCode);
        using var json = JsonDocument.Parse(await exchange.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("access_token").GetString()!;
    }

    private static async Task AssertRejectedAsInvalidAsync(HttpResponseMessage response)
    {
        // OpenIddict answers directly, without sending the user back to the client.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.Contains("invalid_request", await response.Content.ReadAsStringAsync());
    }
}
