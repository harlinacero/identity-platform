using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Identity.IntegrationTests.Infrastructure;

/// <summary>Simulates a slow data store: once enabled, every command waits before it runs.</summary>
public sealed class SlowCommandInterceptor(TimeSpan delay) : DbCommandInterceptor
{
    public bool Enabled { get; set; }

    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        await WaitAsync(cancellationToken);
        return result;
    }

    public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        await WaitAsync(cancellationToken);
        return result;
    }

    public override async ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
    {
        await WaitAsync(cancellationToken);
        return result;
    }

    private Task WaitAsync(CancellationToken cancellationToken) =>
        Enabled ? Task.Delay(delay, cancellationToken) : Task.CompletedTask;
}
