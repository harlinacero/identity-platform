using Identity.Application.Tenancy;

namespace Identity.Api.Middleware;

/// <summary>
/// Sets the tenant from the OIDC client before any tenant-protected data is read. It runs before
/// authentication, because OpenIddict reads its records there. No other tenant input is considered.
/// </summary>
public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IClientDirectory directory, ITenantContextSetter tenant)
    {
        var clientId = await FindClientIdAsync(context);
        if (!string.IsNullOrEmpty(clientId)
            && await directory.FindTenantIdAsync(clientId, context.RequestAborted) is { } tenantId)
        {
            tenant.Set(tenantId);
        }

        await next(context);
    }

    private static async Task<string?> FindClientIdAsync(HttpContext context)
    {
        var path = context.Request.Path;
        if (path.Equals("/connect/authorize", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/connect/token", StringComparison.OrdinalIgnoreCase))
        {
            return await ReadAsync(context, "client_id");
        }

        if (path.Equals("/account/login", StringComparison.OrdinalIgnoreCase)
            && LoginReturnUrl.TryGetClientId(await ReadAsync(context, "returnUrl"), out var clientId))
        {
            return clientId;
        }

        return null;
    }

    private static async Task<string?> ReadAsync(HttpContext context, string name)
    {
        if (context.Request.HasFormContentType)
        {
            var form = await context.Request.ReadFormAsync(context.RequestAborted);
            if (form.TryGetValue(name, out var formValue))
            {
                return formValue.Count == 1 ? formValue[0] : null;
            }
        }

        var queryValue = context.Request.Query[name];
        return queryValue.Count == 1 ? queryValue[0] : null;
    }
}
