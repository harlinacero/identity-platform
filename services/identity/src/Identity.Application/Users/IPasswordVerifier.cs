namespace Identity.Application.Users;

public interface IPasswordVerifier
{
    string Hash(string password);

    bool Verify(string passwordHash, string password);

    /// <summary>Does the same work as <see cref="Verify"/>, so that an unknown user takes as long as a known one.</summary>
    void VerifyAgainstDummy(string password);
}
