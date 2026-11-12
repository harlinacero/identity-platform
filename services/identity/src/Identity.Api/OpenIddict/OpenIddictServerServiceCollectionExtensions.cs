using System.Security.Cryptography.X509Certificates;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Server;
using static OpenIddict.Abstractions.OpenIddictConstants;
using static OpenIddict.Server.OpenIddictServerEvents;
using static OpenIddict.Server.OpenIddictServerHandlers.Protection;

namespace Identity.Api.OpenIddict;

public static class OpenIddictServerServiceCollectionExtensions
{
    private static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan AuthorizationCodeLifetime = TimeSpan.FromMinutes(5);

    public static IServiceCollection AddIdentityOidcServer(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var relaxed = environment.IsDevelopment() || environment.IsEnvironment("Testing");
        var issuer = configuration["Identity:Issuer"]
            ?? throw new InvalidOperationException("Missing required setting 'Identity:Issuer'.");

        services.AddOpenIddict().AddServer(server =>
        {
            server.SetIssuer(new Uri(issuer));
            server.SetAuthorizationEndpointUris("/connect/authorize")
                .SetTokenEndpointUris("/connect/token")
                .SetUserInfoEndpointUris("/connect/userinfo");

            server.AllowAuthorizationCodeFlow().RequireProofKeyForCodeExchange();
            server.RegisterScopes(Scopes.OpenId, Scopes.Profile);
            server.Configure(options => options.CodeChallengeMethods.Remove(CodeChallengeMethods.Plain));

            server.SetAccessTokenLifetime(AccessTokenLifetime);
            server.SetAuthorizationCodeLifetime(AuthorizationCodeLifetime);

            // Signed but not encrypted, so that the Gateway can validate it through JWKS.
            server.DisableAccessTokenEncryption();

            AddKeys(server, configuration, relaxed);

            var aspNetCore = server.UseAspNetCore()
                .EnableAuthorizationEndpointPassthrough()
                .EnableTokenEndpointPassthrough()
                .EnableUserInfoEndpointPassthrough();

            if (relaxed)
            {
                aspNetCore.DisableTransportSecurityRequirement();
            }

            server.AddEventHandler<ValidateTokenContext>(handler => handler
                .UseScopedHandler<TenantTokenValidationHandler>()
                .SetOrder(RestoreTokenEntryProperties.Descriptor.Order - 1)
                .SetType(OpenIddictServerHandlerType.Custom));
        });

        return services;
    }

    private static void AddKeys(OpenIddictServerBuilder server, IConfiguration configuration, bool relaxed)
    {
        if (relaxed)
        {
            server.AddEphemeralSigningKey().AddEphemeralEncryptionKey();
            return;
        }

        server.AddSigningCertificate(LoadCertificate(configuration, "Identity:Signing:CertificateBase64"));
        server.AddEncryptionCertificate(LoadCertificate(configuration, "Identity:Encryption:CertificateBase64"));
    }

    private static X509Certificate2 LoadCertificate(IConfiguration configuration, string key)
    {
        var encoded = configuration[key] ?? throw new InvalidOperationException($"Missing required setting '{key}'.");
        return X509CertificateLoader.LoadPkcs12(Convert.FromBase64String(encoded), password: null);
    }
}
