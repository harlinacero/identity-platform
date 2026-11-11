using Identity.Infrastructure.Persistence;

namespace Identity.Api.Endpoints;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Liveness has no dependencies, so a data store outage never restarts the process.
        endpoints.MapGet("/health/live", () => Results.Ok());
        endpoints.MapGet("/health/ready", GetReadyAsync);
    }

    private static async Task<IResult> GetReadyAsync(DataStoreProbe probe, CancellationToken cancellationToken) =>
        await probe.IsReadyAsync(cancellationToken)
            ? Results.Ok()
            : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
}
