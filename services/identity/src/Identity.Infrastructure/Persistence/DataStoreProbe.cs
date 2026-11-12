using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Identity.Infrastructure.Persistence;

/// <summary>
/// Readiness check for the data store. It opens its own short-lived connection, with no retries,
/// so a probe answers within its budget instead of waiting through the retrying strategy.
/// </summary>
public sealed class DataStoreProbe(IConfiguration configuration)
{
    private const int TimeoutSeconds = 2;

    private readonly string _connectionString = new NpgsqlConnectionStringBuilder(
        configuration.GetConnectionString("Identity")
            ?? throw new InvalidOperationException("Missing required setting 'ConnectionStrings:Identity'."))
    {
        Timeout = TimeoutSeconds,
        CommandTimeout = TimeoutSeconds,
        Pooling = false,
    }.ConnectionString;

    public async Task<bool> IsReadyAsync(CancellationToken cancellationToken)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(TimeSpan.FromSeconds(TimeoutSeconds));

        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(budget.Token);
            await using var command = new NpgsqlCommand("SELECT 1", connection);
            await command.ExecuteScalarAsync(budget.Token);
            return true;
        }
        catch (Exception exception) when (exception is NpgsqlException or OperationCanceledException or TimeoutException
            && !cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }
}
