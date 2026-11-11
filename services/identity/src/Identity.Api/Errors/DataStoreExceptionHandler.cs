using Identity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics;

namespace Identity.Api.Errors;

/// <summary>
/// Reports data store outages and timeouts as a temporary error, without exposing their details.
/// Permanent database errors fall through to the default handler and become a 500.
/// </summary>
public sealed class DataStoreExceptionHandler(ILogger<DataStoreExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (!DataStoreFailures.IsTransient(exception) || context.Response.HasStarted)
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
}
