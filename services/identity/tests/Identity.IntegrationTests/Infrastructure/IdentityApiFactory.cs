using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Identity.IntegrationTests.Infrastructure;

/// <summary>The service in process, connected as identity_app to the test database.</summary>
public sealed class IdentityApiFactory(
    string connectionString,
    string environment = "Testing",
    IReadOnlyDictionary<string, string?>? settings = null,
    TimeProvider? timeProvider = null,
    ILoggerProvider? loggerProvider = null) : WebApplicationFactory<Program>
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

        if (loggerProvider is not null)
        {
            builder.ConfigureLogging(logging => logging.AddProvider(loggerProvider));
        }

        if (timeProvider is not null)
        {
            builder.ConfigureTestServices(services => services.Replace(ServiceDescriptor.Singleton(timeProvider)));
        }
    }

    public HttpClient CreateBrowser() => CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = true,
    });
}
