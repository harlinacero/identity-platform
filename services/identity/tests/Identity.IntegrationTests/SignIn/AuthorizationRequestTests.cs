using System.Net;
using Identity.Infrastructure.Seeding;
using Identity.IntegrationTests.Infrastructure;

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

    private static async Task AssertRejectedAsInvalidAsync(HttpResponseMessage response)
    {
        // OpenIddict answers directly, without sending the user back to the client.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.Contains("invalid_request", await response.Content.ReadAsStringAsync());
    }
}
