using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Identity.IntegrationTests.Infrastructure;

/// <summary>The service in process, connected as identity_app to the test database.</summary>
public sealed class IdentityApiFactory(
    string connectionString,
    string environment = "Testing",
    IReadOnlyDictionary<string, string?>? settings = null) : WebApplicationFactory<Program>
{
    public const string TenantAPassword = "tenant-a-test-password";
    public const string TenantBPassword = "tenant-b-test-password";

    public static IReadOnlyDictionary<string, string?> DefaultSeedSettings { get; } = new Dictionary<string, string?>
    {
        ["Seed:TenantA:UserPassword"] = TenantAPassword,
        ["Seed:TenantB:UserPassword"] = TenantBPassword,
    };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.UseSetting("ConnectionStrings:Identity", connectionString);
        builder.UseSetting("Identity:Issuer", "http://localhost/");

        foreach (var (key, value) in settings ?? DefaultSeedSettings)
        {
            builder.UseSetting(key, value);
        }
    }

    public HttpClient CreateBrowser() => CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = true,
    });
}
