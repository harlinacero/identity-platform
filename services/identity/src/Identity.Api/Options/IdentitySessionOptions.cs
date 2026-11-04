namespace Identity.Api.Options;

public sealed class IdentitySessionOptions
{
    public const string Section = "Identity:Session";

    public TimeSpan Duration { get; set; } = TimeSpan.FromMinutes(15);
}
