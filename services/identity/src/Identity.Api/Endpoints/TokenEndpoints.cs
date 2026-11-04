using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;

namespace Identity.Api.Endpoints;

public static class TokenEndpoints
{
    public static void MapTokenEndpoints(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost("/connect/token", ExchangeAsync);

    private static async Task<IResult> ExchangeAsync(HttpContext context, CancellationToken _)
    {
        var request = context.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request is missing.");

        if (!request.IsAuthorizationCodeGrantType())
        {
            throw new InvalidOperationException("Only the authorization code grant is enabled.");
        }

        // OpenIddict has already validated the code, its client and the PKCE proof.
        var result = await context.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        return Results.SignIn(result.Principal!, authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }
}
