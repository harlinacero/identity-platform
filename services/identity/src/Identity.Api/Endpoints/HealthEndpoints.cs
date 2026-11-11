using Identity.Infrastructure.Persistence;

namespace Identity.Api.Endpoints;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet("/health", GetAsync);

    private static async Task<IResult> GetAsync(IdentityDbContext context, CancellationToken cancellationToken) =>
        await context.Database.CanConnectAsync(cancellationToken)
            ? Results.Ok()
            : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
}
