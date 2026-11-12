using System.Security.Cryptography.X509Certificates;
using Identity.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Identity.IntegrationTests.SignIn;

/// <summary>The __Host- prefix stops a sibling subdomain from planting its own session (cookie tossing).</summary>
[Collection(PostgresCollection.Name)]
public sealed class SessionCookieTests(PostgresFixture database)
{
    [Fact]
    public async Task SessionCookie_WhenProduction_ShouldUseHostPrefix()
    {
        // Arrange
        var settings = new Dictionary<string, string?>(IdentityApiFactory.DefaultSeedSettings)
        {
            ["Identity:Signing:CertificateBase64"] = TestCertificates.CreateBase64(X509KeyUsageFlags.DigitalSignature),
            ["Identity:Encryption:CertificateBase64"] = TestCertificates.CreateBase64(X509KeyUsageFlags.KeyEncipherment),
        };
        await using var factory = new IdentityApiFactory(database.AppConnectionString, "Production", settings);

        // Act
        var cookie = CookieOf(factory);

        // Assert
        Assert.Equal("__Host-identity.session", cookie.Name);
        Assert.Equal(CookieSecurePolicy.Always, cookie.SecurePolicy);
        Assert.Equal("/", cookie.Path);
        Assert.Null(cookie.Domain);
    }

    [Fact]
    public async Task SessionCookie_WhenTesting_ShouldUsePlainName()
    {
        // Arrange: browsers reject __Host- cookies over plain HTTP, which local runs use.
        await using var factory = new IdentityApiFactory(database.AppConnectionString);

        // Act
        var cookie = CookieOf(factory);

        // Assert
        Assert.Equal("identity.session", cookie.Name);
    }

    private static CookieBuilder CookieOf(IdentityApiFactory factory) =>
        factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme)
            .Cookie;
}
