using Identity.Application.Tenancy;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace Identity.Api.OpenIddict;

/// <summary>
/// Runs after the token's signature is validated and before OpenIddict first reads its entry.
/// Tokens must carry a tenant, which must match the client's tenant when one was already resolved.
/// </summary>
public sealed class TenantTokenValidationHandler(ITenantContext current, ITenantContextSetter setter)
    : IOpenIddictServerHandler<OpenIddictServerEvents.ValidateTokenContext>
{
    public ValueTask HandleAsync(OpenIddictServerEvents.ValidateTokenContext context)
    {
        if (context.IsRejected || context.Principal is null)
        {
            return ValueTask.CompletedTask;
        }

        if (!Guid.TryParse(context.Principal.GetClaim(TenantClaims.TenantId), out var tenantId))
        {
            Reject(context, "The token has no valid tenant.");
            return ValueTask.CompletedTask;
        }

        if (current.TenantId is { } resolved && resolved != tenantId)
        {
            Reject(context, "The token belongs to another tenant.");
            return ValueTask.CompletedTask;
        }

        setter.Set(tenantId);
        return ValueTask.CompletedTask;
    }

    private static void Reject(OpenIddictServerEvents.ValidateTokenContext context, string description) =>
        context.Reject(
            error: context.EndpointType is OpenIddictServerEndpointType.Token ? OpenIddictConstants.Errors.InvalidGrant : OpenIddictConstants.Errors.InvalidToken,
            description: description);
}
