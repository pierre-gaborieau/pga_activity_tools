using Microsoft.EntityFrameworkCore;
using pgaActivityTools.Common.OpenApi;
using pgaActivityTools.Data;
using pgaActivityTools.Models.Database;
using pgaActivityTools.Models.Strava;

namespace pgaActivityTools.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/auth")
            .WithTags(OpenApiTags.Auth.ToString())
            .WithOpenApi();

        group.MapGet("", OAuthBegin)
            .WithName("StartStravaAuth");

        group.MapGet("/callback", OAuthCallback)
            .WithName("StravaAuthCallback");


        return routes;
    }

    private static IResult OAuthBegin(IConfiguration configuration)
    {
        var clientId = configuration["Strava:ClientId"];
        var baseUrl = configuration["Application:BaseUrl"];
        var redirectUri = "http://localhost:5148/auth/callback";
        var scope = "activity:read_all,activity:write";

        var authUrl = $"https://www.strava.com/oauth/authorize?client_id={clientId}&redirect_uri={redirectUri}&response_type=code&approval_prompt=force&scope={scope}";

        return Results.Redirect(authUrl);
    }

    private static async Task<IResult> OAuthCallback(string code,
    IConfiguration configuration,
    IServiceScopeFactory scopeFactory,
    ILogger<Program> logger)
    {

        var clientId = configuration["Strava:ClientId"];
        var clientSecret = configuration["Strava:ClientSecret"];

        using var httpClient = new HttpClient();

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
    {
        { "client_id", clientId ?? "" },
        { "client_secret", clientSecret ?? "" },
        { "code", code },
        { "grant_type", "authorization_code" }
    });

        try
        {
            var response = await httpClient.PostAsync("https://www.strava.com/oauth/token", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            logger.LogInformation("OAuth response: {Response}", responseContent);

            var tokenResponse = await response.Content.ReadFromJsonAsync<StravaTokenResponse>();

            if (tokenResponse != null)
            {
                // Sauvegarder le token en mémoire
                using var scope = scopeFactory.CreateScope();

                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                bool isAthleteWhitelisted = await db.AthleteWhitelist
                    .AnyAsync(a => a.AthleteId == tokenResponse.Athlete.Id);
                if (!isAthleteWhitelisted)
                {
                    logger.LogWarning("❌ Athlete {AthleteId} is not whitelisted. Authorization denied.",
                        tokenResponse.Athlete.Id);
                    return Results.Content($@"
                <html>
                <meta charset='UTF-8'>
                <head><title>Authorization Denied</title></head>
                <body>
                    <h1>❌ Authorization denied!</h1>
                    <p>Your athlete ID ({tokenResponse.Athlete.Id}) is not whitelisted to use this application.</p>
                    <hr>
                    <p>Contact the administrator to request access.</p>
                </body>
                </html>
            ", "text/html");
                }


                var existingToken = await db.AthleteTokens
                    .FirstOrDefaultAsync(t => t.AthleteId == tokenResponse.Athlete.Id);
                if (existingToken != null)
                {
                    existingToken.AccessToken = tokenResponse.Access_token;
                    existingToken.RefreshToken = tokenResponse.Refresh_token;
                    existingToken.ExpiresAt = DateTime.SpecifyKind(
                            DateTimeOffset.FromUnixTimeSeconds(tokenResponse.Expires_at).DateTime,
                            DateTimeKind.Utc);
                    existingToken.UpdatedAt = DateTime.UtcNow;
                    db.AthleteTokens.Update(existingToken);
                }
                else
                {
                    var newToken = new AthleteToken
                    {
                        AthleteId = tokenResponse.Athlete.Id,
                        AccessToken = tokenResponse.Access_token,
                        RefreshToken = tokenResponse.Refresh_token,
                        ExpiresAt = DateTime.SpecifyKind(
                            DateTimeOffset.FromUnixTimeSeconds(tokenResponse.Expires_at).DateTime,
                            DateTimeKind.Utc),
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    await db.AthleteTokens.AddAsync(newToken);
                }
                await db.SaveChangesAsync();

                logger.LogInformation("✅ User authorized: {Username} (ID: {AthleteId})",
                    tokenResponse.Athlete.Username, tokenResponse.Athlete.Id);
                logger.LogInformation("Token stored for athlete {AthleteId}", tokenResponse.Athlete.Id);

                return Results.Content($@"
                <html>
                <meta charset='UTF-8'>
                <head><title>Authorization Successful</title></head>
                <body>
                    <h1>✅ Authorization successful!</h1>
                    <p><strong>Athlete:</strong> {tokenResponse.Athlete.Firstname} {tokenResponse.Athlete.Lastname}</p>
                    <p><strong>Athlete ID:</strong> {tokenResponse.Athlete.Id}</p>
                    <p><strong>Access Token:</strong> {tokenResponse.Access_token[..20]}...</p>
                    <p><strong>Expires at:</strong> {DateTimeOffset.FromUnixTimeSeconds(tokenResponse.Expires_at).DateTime}</p>
                    <hr>
                    <p>✅ Votre token a été sauvegardé!</p>
                    <p>Votre application recevra maintenant les webhooks pour vos nouvelles activités avec la météo!</p>
                    <p>Fermez cette fenêtre et uploadez une activité depuis l'app mobile Strava.</p>
                </body>
                </html>
            ", "text/html");
            }

            return Results.BadRequest("Failed to get access token");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during OAuth callback");
            return Results.Problem(ex.Message);
        }
    }
}