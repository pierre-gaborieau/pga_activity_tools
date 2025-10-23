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

       // ✅ GET pour retourner le body JSON
        group.MapGet("/health", async (ApplicationDbContext dbContext, IVersion version) => 
                await GetHealth(dbContext, version))
            .WithName("GetHealthStatus");

        // ✅ HEAD pour retourner seulement les headers (pas de body)
        group.MapMethods("/healthz", new[] { HttpMethods.Head }, async (ApplicationDbContext dbContext) =>
                await GetHealthHead(dbContext))
            .WithName("GetReadinessStatus");

        return routes;
    }

    private static IResult GetVersion(IVersion version)
    {
        var versionInfo = version.GetVersionInfo();
        return Results.Ok(versionInfo);
    }

    private static async Task<IResult> GetHealthHead(ApplicationDbContext dbContext)
    {
        try
        {
            var canConnect = await dbContext.Database.CanConnectAsync();
            return canConnect ? Results.Ok() : Results.StatusCode(503);
        }
        catch (Exception)
        {
            return Results.StatusCode(503);
        }
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
