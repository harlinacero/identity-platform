namespace Identity.Api.Options;

public sealed class IdentityRequestOptions
{
    public const string Section = "Identity:Requests";

    /// <summary>Time budget for a whole request, retries included. On expiry the request ends with 503.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);
}
