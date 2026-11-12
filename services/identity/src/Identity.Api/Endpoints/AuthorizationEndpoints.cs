using System.Security.Claims;
using Identity.Application.Tenancy;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Identity.Api.Endpoints;

public static class AuthorizationEndpoints
{
    private const string Audience = "identity-platform";

    public static void MapAuthorizationEndpoints(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapMethods("/connect/authorize", [HttpMethods.Get, HttpMethods.Post], AuthorizeAsync);

    private static async Task<IResult> AuthorizeAsync(HttpContext context, ITenantContext tenant)
    {
        var request = context.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request is missing.");

        var session = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        var sessionTenant = session.Principal?.FindFirstValue(TenantClaims.TenantId);

        // A session is valid only for the tenant in which it started.
        if (!session.Succeeded || sessionTenant != tenant.TenantId?.ToString())
        {
            // The login page returns here with a GET, so a POSTed request is carried over as a query string.
            var parameters = HttpMethods.IsPost(context.Request.Method) && context.Request.HasFormContentType
                ? QueryString.Create(await context.Request.ReadFormAsync(context.RequestAborted))
                : context.Request.QueryString;

            return Results.Challenge(
                new AuthenticationProperties { RedirectUri = context.Request.PathBase + context.Request.Path + parameters },
                [CookieAuthenticationDefaults.AuthenticationScheme]);
        }

        var identity = new ClaimsIdentity(
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            Claims.Name,
            Claims.Role);

        identity.AddClaim(new Claim(Claims.Subject, session.Principal!.FindFirstValue(Claims.Subject)!)
            .SetDestinations(Destinations.AccessToken, Destinations.IdentityToken));
        identity.AddClaim(new Claim(TenantClaims.TenantId, sessionTenant!)
            .SetDestinations(Destinations.AccessToken, Destinations.IdentityToken));

        // OIDC releases the username only with the profile scope.
        if (request.HasScope(Scopes.Profile))
        {
            identity.AddClaim(new Claim(Claims.PreferredUsername, session.Principal!.FindFirstValue(Claims.PreferredUsername)!)
                .SetDestinations(Destinations.AccessToken, Destinations.IdentityToken));
        }

        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(request.GetScopes());
        principal.SetResources(Audience);

        return Results.SignIn(principal, authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }
}
