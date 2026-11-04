using Microsoft.AspNetCore.WebUtilities;

namespace Identity.Api.Middleware;

public static class LoginReturnUrl
{
    private const string AuthorizePath = "/connect/authorize";

    /// <summary>Accepts only a local URL to the authorization endpoint, which prevents open redirects.</summary>
    public static bool TryGetClientId(string? returnUrl, out string clientId)
    {
        clientId = string.Empty;
        if (string.IsNullOrEmpty(returnUrl))
        {
            return false;
        }

        var parts = returnUrl.Split('?', 2);
        if (!string.Equals(parts[0], AuthorizePath, StringComparison.Ordinal) || parts.Length != 2)
        {
            return false;
        }

        if (!QueryHelpers.ParseQuery(parts[1]).TryGetValue("client_id", out var values) || values.Count != 1)
        {
            return false;
        }

        clientId = values[0] ?? string.Empty;
        return clientId.Length > 0;
    }
}
