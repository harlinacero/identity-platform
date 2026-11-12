using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Identity.Infrastructure.Persistence;

public static class DataStoreFailures
{
    /// <summary>
    /// True for outages and timeouts, which a caller may retry later. Errors raised by the server,
    /// such as constraint or RLS violations, are permanent and must not look like an outage.
    /// </summary>
    public static bool IsTransient(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            switch (current)
            {
                case RetryLimitExceededException or TimeoutException:
                    return true;
                case PostgresException postgres:
                    return postgres.IsTransient;
                case NpgsqlException npgsql when npgsql.IsTransient:
                    return true;
            }
        }

        return false;
    }
}
