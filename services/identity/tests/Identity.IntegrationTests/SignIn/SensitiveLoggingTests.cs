using System.Text.Json;
using Identity.Infrastructure.Seeding;
using Identity.IntegrationTests.Infrastructure;

namespace Identity.IntegrationTests.SignIn;

[Collection(PostgresCollection.Name)]
public sealed class SensitiveLoggingTests(PostgresFixture database)
{
    [Fact]
    public async Task Logs_WhenSignInCompletes_ShouldNotContainSecrets()
    {
        // Arrange
        var logs = new CapturingLoggerProvider();
        await using var factory = new IdentityApiFactory(database.AppConnectionString, loggerProvider: logs);
        var client = new OidcTestClient(factory.CreateBrowser());

        // Act
        var code = await client.SignInForCodeAsync(SeedData.ClientAId, SeedData.Username, IdentityApiFactory.TenantAPassword);
        var exchange = await client.ExchangeAsync(SeedData.ClientAId, code);
        using var json = JsonDocument.Parse(await exchange.Content.ReadAsStringAsync());
        var accessToken = json.RootElement.GetProperty("access_token").GetString()!;
        await client.UserInfoAsync(accessToken);

        // Assert
        var allLogs = string.Join('\n', logs.Messages);
        Assert.NotEmpty(logs.Messages);
        Assert.DoesNotContain(client.Verifier, allLogs);
        Assert.DoesNotContain(code, allLogs);
        Assert.DoesNotContain(accessToken, allLogs);
        Assert.DoesNotContain(IdentityApiFactory.TenantAPassword, allLogs);
    }
}
