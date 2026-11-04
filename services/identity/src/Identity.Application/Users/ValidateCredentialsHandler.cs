using Identity.Domain.Users;

namespace Identity.Application.Users;

public sealed class ValidateCredentialsHandler(ILocalUserRepository users, IPasswordVerifier passwords)
{
    public async Task<LocalUser?> HandleAsync(string username, string password, CancellationToken cancellationToken)
    {
        var user = await users.FindByUsernameAsync(username, cancellationToken);
        if (user is null)
        {
            passwords.VerifyAgainstDummy(password);
            return null;
        }

        return passwords.Verify(user.PasswordHash, password) ? user : null;
    }
}
