using System.Diagnostics;
using System.Net;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Seeding;
using Identity.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.IntegrationTests.Resilience;

[Collection(PostgresCollection.Name)]
public sealed class SlowDataStoreTests(PostgresFixture database)
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan Margin = TimeSpan.FromSeconds(4);
    private readonly SlowCommandInterceptor _slowDataStore = new(TimeSpan.FromSeconds(30));

    [Fact]
    public async Task Login_WhenDataStoreSlow_ShouldReturnTemporaryErrorWithinBudget()
    {
        // Arrange
        await using var factory = CreateFactory();
        var client = new OidcTestClient(factory.CreateBrowser());
        var loginUrl = OidcTestClient.LocationOf(await client.StartAsync(SeedData.ClientAId));
        var form = await client.ReadLoginFormAsync(loginUrl);
        _slowDataStore.Enabled = true;
        var stopwatch = Stopwatch.StartNew();

        // Act
        var response = await client.PostLoginAsync(form, SeedData.Username, IdentityApiFactory.TenantAPassword);

        // Assert
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.True(stopwatch.Elapsed < RequestTimeout + Margin, $"The request took {stopwatch.Elapsed}.");
    }

    [Fact]
    public async Task Exchange_WhenDataStoreSlow_ShouldReturnTemporaryErrorWithinBudget()
    {
        // Arrange
        await using var factory = CreateFactory();
        var client = new OidcTestClient(factory.CreateBrowser());
        var code = await client.SignInForCodeAsync(SeedData.ClientAId, SeedData.Username, IdentityApiFactory.TenantAPassword);
        _slowDataStore.Enabled = true;
        var stopwatch = Stopwatch.StartNew();

        // Act
        var response = await client.ExchangeAsync(SeedData.ClientAId, code);

        // Assert
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.DoesNotContain("access_token", await response.Content.ReadAsStringAsync());
        Assert.True(stopwatch.Elapsed < RequestTimeout + Margin, $"The request took {stopwatch.Elapsed}.");
    }

    private IdentityApiFactory CreateFactory() => new(
        database.AppConnectionString,
        settings: new Dictionary<string, string?>(IdentityApiFactory.DefaultSeedSettings)
        {
            ["Identity:Requests:Timeout"] = RequestTimeout.ToString(),
        },
        configureServices: services => services.ConfigureDbContext<IdentityDbContext>(
            (_, options) => options.AddInterceptors(_slowDataStore)));
}
