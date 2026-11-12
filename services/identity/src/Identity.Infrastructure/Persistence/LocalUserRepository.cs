using Identity.Application.Users;
using Identity.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence;

public sealed class LocalUserRepository(IdentityDbContext context) : ILocalUserRepository
{
    public Task<LocalUser?> FindByUsernameAsync(string username, CancellationToken cancellationToken)
    {
        var normalized = LocalUser.Normalize(username);
        return context.LocalUsers.SingleOrDefaultAsync(user => user.NormalizedUsername == normalized, cancellationToken);
    }
}
