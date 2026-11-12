using Identity.Api.Endpoints;
using Identity.Api.Errors;
using Identity.Api.Middleware;
using Identity.Api.OpenIddict;
using Identity.Api.Options;
using Identity.Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.Timeouts;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddIdentityInfrastructure();
builder.Services.AddIdentityOidcServer(builder.Configuration, builder.Environment);
builder.Services.AddAntiforgery();

var relaxedTransport = builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing");
var session = builder.Configuration.GetSection(IdentitySessionOptions.Section).Get<IdentitySessionOptions>() ?? new IdentitySessionOptions();
var requests = builder.Configuration.GetSection(IdentityRequestOptions.Section).Get<IdentityRequestOptions>() ?? new IdentityRequestOptions();

// Bounds every request, retries included, so a slow data store cannot hold requests and connections for minutes.
builder.Services.AddRequestTimeouts(options => options.DefaultPolicy = new RequestTimeoutPolicy
{
    Timeout = requests.Timeout,
    TimeoutStatusCode = StatusCodes.Status503ServiceUnavailable,
});

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        // __Host- requires HTTPS, so local runs over plain HTTP keep the unprefixed name.
        options.Cookie.Name = relaxedTransport ? "identity.session" : "__Host-identity.session";
        options.Cookie.Path = "/";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = relaxedTransport ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        options.LoginPath = "/account/login";
        options.ReturnUrlParameter = "returnUrl";
        options.ExpireTimeSpan = session.Duration;
        options.SlidingExpiration = false;
    });
builder.Services.AddAuthorization();
builder.Services.AddExceptionHandler<DataStoreExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();
app.UseRequestTimeouts();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapAuthorizationEndpoints();
app.MapTokenEndpoints();
app.MapUserInfoEndpoints();
app.MapLoginEndpoints();
app.MapHealthEndpoints();

app.Run();

public partial class Program;
