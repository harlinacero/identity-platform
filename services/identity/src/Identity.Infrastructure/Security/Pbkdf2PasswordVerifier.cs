using Identity.Application.Users;
using Identity.Domain.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Identity.Infrastructure.Security;

/// <summary>PBKDF2-HMAC-SHA512 with the iteration count from current OWASP guidance.</summary>
public sealed class Pbkdf2PasswordVerifier : IPasswordVerifier
{
    private const int Iterations = 210_000;

    // Internal to the hasher's API: the user argument is not part of the hash.
    private static readonly LocalUser Unused = LocalUser.Create("unused", "unused");

    private readonly PasswordHasher<LocalUser> _hasher = new(Options.Create(new PasswordHasherOptions
    {
        CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3,
        IterationCount = Iterations,
    }));

    private readonly Lazy<string> _dummyHash;

    public Pbkdf2PasswordVerifier()
    {
        _dummyHash = new Lazy<string>(() => Hash(Guid.NewGuid().ToString()));
    }

    public string Hash(string password) => _hasher.HashPassword(Unused, password);

    public bool Verify(string passwordHash, string password) =>
        _hasher.VerifyHashedPassword(Unused, passwordHash, password) != PasswordVerificationResult.Failed;

    public void VerifyAgainstDummy(string password) => _ = Verify(_dummyHash.Value, password);
}
