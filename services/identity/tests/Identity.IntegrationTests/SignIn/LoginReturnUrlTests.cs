using System.Net;
using Identity.IntegrationTests.Infrastructure;

namespace Identity.IntegrationTests.SignIn;

[Collection(PostgresCollection.Name)]
public sealed class LoginReturnUrlTests(PostgresFixture database)
{
    [Theory]
    [InlineData("https://attacker.example/connect/authorize?client_id=tenant-a-web")]
    [InlineData("//attacker.example/connect/authorize?client_id=tenant-a-web")]
    [InlineData("/account/profile?client_id=tenant-a-web")]
    [InlineData("/connect/authorize")]
    [InlineData("")]
    public async Task Login_WhenReturnUrlIsNotLocalAuthorize_ShouldReject(string returnUrl)
    {
        // Arrange
        await using var factory = new IdentityApiFactory(database.AppConnectionString);
        var browser = factory.CreateBrowser();

        // Act
        var response = await browser.GetAsync($"/account/login?returnUrl={Uri.EscapeDataString(returnUrl)}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
