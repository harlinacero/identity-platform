using System.Security.Claims;
using Identity.Application.Tenancy;
using Microsoft.AspNetCore.Authentication;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Identity.Api.Endpoints;

public static class UserInfoEndpoints
{
    public static void MapUserInfoEndpoints(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapMethods("/connect/userinfo", [HttpMethods.Get, HttpMethods.Post], GetAsync);

    private static async Task<IResult> GetAsync(HttpContext context, CancellationToken _)
    {
        // OpenIddict has already validated the access token.
        var result = await context.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        var principal = result.Principal!;

        // Returns only the claims the token carries, which already reflect the granted scopes.
        var claims = new[] { Claims.Subject, TenantClaims.TenantId, Claims.PreferredUsername }
            .Select(type => (Type: type, Value: principal.FindFirstValue(type)))
            .Where(claim => claim.Value is not null)
            .ToDictionary(claim => claim.Type, claim => claim.Value);

        return Results.Json(claims);
    }
}
