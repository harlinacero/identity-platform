using System.Security.Claims;
using System.Text.Encodings.Web;
using Identity.Api.Middleware;
using Identity.Application.Tenancy;
using Identity.Application.Users;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Identity.Api.Endpoints;

/// <summary>A temporary minimal form, to be replaced by the React sign-in experience.</summary>
public static class LoginEndpoints
{
    private const string GenericFailure = "The username or password is not valid.";

    public static void MapLoginEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/account/login", ShowForm);
        endpoints.MapPost("/account/login", SubmitAsync).DisableAntiforgery();
    }

    private static IResult ShowForm(HttpContext context, IAntiforgery antiforgery, [FromQuery] string? returnUrl) =>
        LoginReturnUrl.TryGetClientId(returnUrl, out _)
            ? RenderForm(context, antiforgery, returnUrl!, error: null)
            : Results.BadRequest();

    private static async Task<IResult> SubmitAsync(
        HttpContext context,
        IAntiforgery antiforgery,
        ITenantContext tenant,
        ValidateCredentialsHandler credentials,
        ILogger<ValidateCredentialsHandler> logger)
    {
        await antiforgery.ValidateRequestAsync(context);

        var form = await context.Request.ReadFormAsync(context.RequestAborted);
        var returnUrl = form["returnUrl"].ToString();
        if (!LoginReturnUrl.TryGetClientId(returnUrl, out var clientId))
        {
            return Results.BadRequest();
        }

        // An unknown client has no tenant, and therefore no users to match.
        var user = tenant.TenantId is null
            ? null
            : await credentials.HandleAsync(form["username"].ToString(), form["password"].ToString(), context.RequestAborted);

        if (user is null)
        {
            logger.LogInformation("Failed sign-in for client {ClientId} in tenant {TenantId}", clientId, tenant.TenantId);
            return RenderForm(context, antiforgery, returnUrl, GenericFailure);
        }

        var identity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme);
        identity.AddClaim(new Claim(Claims.Subject, user.Id.ToString()));
        identity.AddClaim(new Claim(TenantClaims.TenantId, user.TenantId.ToString()));
        identity.AddClaim(new Claim(Claims.PreferredUsername, user.Username));

        await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
        return Results.LocalRedirect(returnUrl);
    }

    private static IResult RenderForm(HttpContext context, IAntiforgery antiforgery, string returnUrl, string? error)
    {
        var tokens = antiforgery.GetAndStoreTokens(context);
        var encoder = HtmlEncoder.Default;
        var errorHtml = error is null ? string.Empty : $"<p role=\"alert\">{encoder.Encode(error)}</p>";

        return Results.Content(
            $$"""
            <!doctype html>
            <html lang="en">
            <head><meta charset="utf-8"><title>Sign in</title></head>
            <body>
              <h1>Sign in</h1>
              {{errorHtml}}
              <form method="post" action="/account/login">
                <input type="hidden" name="{{encoder.Encode(tokens.FormFieldName)}}" value="{{encoder.Encode(tokens.RequestToken!)}}">
                <input type="hidden" name="returnUrl" value="{{encoder.Encode(returnUrl)}}">
                <label>Username <input name="username" autocomplete="username" required></label>
                <label>Password <input name="password" type="password" autocomplete="current-password" required></label>
                <button type="submit">Sign in</button>
              </form>
            </body>
            </html>
            """,
            "text/html; charset=utf-8");
    }
}
