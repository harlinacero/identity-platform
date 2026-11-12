using System.Net;
using System.Text.RegularExpressions;
using Identity.Infrastructure.Seeding;
using Identity.IntegrationTests.Infrastructure;

namespace Identity.IntegrationTests.SignIn;

[Collection(PostgresCollection.Name)]
public sealed class CredentialsTests(PostgresFixture database)
{
    [Fact]
    public async Task Login_WhenPasswordWrong_ShouldShowGenericErrorAndAllowRetry()
    {
        // Arrange
        await using var factory = new IdentityApiFactory(database.AppConnectionString);
        var client = new OidcTestClient(factory.CreateBrowser());
        var loginUrl = OidcTestClient.LocationOf(await client.StartAsync(SeedData.ClientAId));

        // Act
        var wrong = await client.SubmitCredentialsAsync(loginUrl, SeedData.Username, "wrong-password");
        var right = await client.SubmitCredentialsAsync(loginUrl, SeedData.Username, IdentityApiFactory.TenantAPassword);

        // Assert
        Assert.Equal(HttpStatusCode.OK, wrong.StatusCode);
        Assert.Contains("not valid", await wrong.Content.ReadAsStringAsync());
        Assert.DoesNotContain(wrong.Headers, header => header.Key == "Set-Cookie" && header.Value.Any(value => value.StartsWith("identity.session")));
        Assert.Equal(HttpStatusCode.Redirect, right.StatusCode);
    }

    [Fact]
    public async Task Login_WhenPasswordBelongsToSameUsernameInOtherTenant_ShouldFail()
    {
        // Arrange
        await using var factory = new IdentityApiFactory(database.AppConnectionString);
        var client = new OidcTestClient(factory.CreateBrowser());
        var loginUrl = OidcTestClient.LocationOf(await client.StartAsync(SeedData.ClientAId));

        // Act
        var response = await client.SubmitCredentialsAsync(loginUrl, SeedData.Username, IdentityApiFactory.TenantBPassword);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("not valid", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Login_WhenUsernameUnknown_ShouldReturnSameResponseAsWrongPassword()
    {
        // Arrange
        await using var factory = new IdentityApiFactory(database.AppConnectionString);
        var client = new OidcTestClient(factory.CreateBrowser());
        var loginUrl = OidcTestClient.LocationOf(await client.StartAsync(SeedData.ClientAId));

        // Act
        var wrongPassword = await client.SubmitCredentialsAsync(loginUrl, SeedData.Username, "wrong-password");
        var unknownUser = await client.SubmitCredentialsAsync(loginUrl, "nobody.here", "wrong-password");

        // Assert
        Assert.Equal(wrongPassword.StatusCode, unknownUser.StatusCode);
        Assert.Equal(WithoutAntiforgery(await wrongPassword.Content.ReadAsStringAsync()), WithoutAntiforgery(await unknownUser.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task Login_WhenAntiforgeryTokenMissing_ShouldReject()
    {
        // Arrange
        await using var factory = new IdentityApiFactory(database.AppConnectionString);
        var browser = factory.CreateBrowser();
        var client = new OidcTestClient(browser);

        // Act
        var response = await browser.PostAsync("/account/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["username"] = SeedData.Username,
            ["password"] = IdentityApiFactory.TenantAPassword,
            ["returnUrl"] = client.AuthorizeUrl(SeedData.ClientAId),
        }));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static string WithoutAntiforgery(string html) =>
        Regex.Replace(html, "name=\"__RequestVerificationToken\" value=\"[^\"]*\"", string.Empty);
}
