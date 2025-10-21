using pgaActivityTools.Common.OpenApi;
using pgaActivityTools.Data;
using pgaActivityTools.Services.Version;

namespace pgaActivityTools.Endpoints;

public static class SystemEndpoints
{
    public static IEndpointRouteBuilder MapSystemEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/")
            .WithTags(OpenApiTags.System.ToString())
            .AllowAnonymous()
            .WithOpenApi();

        group.MapGet("/version", (IVersion version) => GetVersion(version))
            .WithName("GetVersionInfo");

        group.MapGet("/health", async (ApplicationDbContext dbContext, IVersion version) => await GetHealth(dbContext, version))
            .WithName("GetHealthStatus");

        return routes;
    }

    private static IResult GetVersion(IVersion version)
    {
        var versionInfo = version.GetVersionInfo();
        return Results.Ok(versionInfo);
    }

    private static async Task<IResult> GetHealth(ApplicationDbContext dbContext, IVersion version)
    {
        try
        {
            var canConnect = await dbContext.Database.CanConnectAsync();

            var health = new
            {
                status = canConnect ? "healthy" : "unhealthy",
                database = canConnect ? "connected" : "disconnected",
                version = version.GetVersionInfo(),
                timestamp = DateTime.UtcNow,
            };
            return canConnect ? Results.Ok(health) : Results.Problem("Database connection failed");
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message);
        }
    }
}
