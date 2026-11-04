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

        return Results.Json(new Dictionary<string, string?>
        {
            [Claims.Subject] = principal.FindFirstValue(Claims.Subject),
            [TenantClaims.TenantId] = principal.FindFirstValue(TenantClaims.TenantId),
            [Claims.PreferredUsername] = principal.FindFirstValue(Claims.PreferredUsername),
        });
    }
}
