using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Identity.Api.Errors;

/// <summary>Reports data store failures as a temporary error, without exposing their details.</summary>
public sealed class DataStoreExceptionHandler(ILogger<DataStoreExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (!IsDataStoreFailure(exception) || context.Response.HasStarted)
        {
            return false;
        }

        logger.LogError(exception, "The data store is unavailable");

        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        await context.Response.WriteAsJsonAsync(
            new { title = "The service is temporarily unavailable. Try again later." },
            cancellationToken);
        return true;
    }

    private static bool IsDataStoreFailure(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is NpgsqlException or RetryLimitExceededException)
            {
                return true;
            }
        }

        return false;
    }
}
