using Identity.Domain.Users;

namespace Identity.Application.Users;

public interface ILocalUserRepository
{
    /// <summary>Finds a user within the current tenant.</summary>
    Task<LocalUser?> FindByUsernameAsync(string username, CancellationToken cancellationToken);
}
