using Identity.Api.Endpoints;
using Identity.Api.Middleware;
using Identity.Api.OpenIddict;
using Identity.Api.Options;
using Identity.Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddIdentityInfrastructure();
builder.Services.AddIdentityOidcServer(builder.Configuration, builder.Environment);
builder.Services.AddAntiforgery();

var relaxedTransport = builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing");
var session = builder.Configuration.GetSection(IdentitySessionOptions.Section).Get<IdentitySessionOptions>() ?? new IdentitySessionOptions();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "identity.session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = relaxedTransport ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        options.LoginPath = "/account/login";
        options.ReturnUrlParameter = "returnUrl";
        options.ExpireTimeSpan = session.Duration;
        options.SlidingExpiration = false;
    });
builder.Services.AddAuthorization();

var app = builder.Build();

app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapAuthorizationEndpoints();
app.MapTokenEndpoints();
app.MapUserInfoEndpoints();
app.MapLoginEndpoints();

app.Run();

public partial class Program;
