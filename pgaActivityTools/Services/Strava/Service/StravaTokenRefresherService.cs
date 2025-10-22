using Microsoft.EntityFrameworkCore;
using pgaActivityTools.Data;
using pgaActivityTools.Models.Database;
using pgaActivityTools.Models.Strava;

namespace pgaActivityTools.Services.Strava.Service;

public class StravaTokenRefresherService : IStravaTokenRefresher
{

    private readonly ILogger<StravaTokenRefresherService> _logger;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;

    public StravaTokenRefresherService(
        ILogger<StravaTokenRefresherService> logger,
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        HttpClient httpClient)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _httpClient = httpClient;
    }


    public async Task<string?> GetValidAccessTokenAsync(long athleteId)
    {
        using var scope = _scopeFactory.CreateScope();
        var _dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var token = await _dbContext.AthleteTokens
            .Where(at => at.AthleteId == athleteId)
            .FirstOrDefaultAsync();

        if (token == null)
        {
            _logger.LogWarning("No valid token found for athlete {AthleteId}", athleteId);
            return null;
        }

        if (token.ExpiresAt > DateTime.UtcNow.AddMinutes(5))
        {
            // Token is still valid
            _logger.LogInformation("Using existing valid token for athlete {AthleteId}", athleteId);
            return token.AccessToken;
        }

        _logger.LogInformation("Refreshing token for athlete {AthleteId}", athleteId);
        return await RefreshAccessTokenAsync(token);
    }

    private async Task<string?> RefreshAccessTokenAsync(AthleteToken token)
    {
        using var scope = _scopeFactory.CreateScope();
        var _dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var clientId = _configuration["Strava:ClientId"];
        var clientSecret = _configuration["Strava:ClientSecret"];

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "client_id", clientId ?? "" },
            { "client_secret", clientSecret ?? "" },
            { "grant_type", "refresh_token" },
            { "refresh_token", token.RefreshToken }
        });

        try
        {
            var response = await _httpClient.PostAsync("https://www.strava.com/oauth/token", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to refresh token for athlete {AthleteId}: {Response}", token.AthleteId, responseContent);
                return null;
            }

            var tokenResponse = await response.Content.ReadFromJsonAsync<StravaTokenResponse>();
            if (tokenResponse == null)
            {
                _logger.LogError("Failed to deserialize token response for athlete {AthleteId}", token.AthleteId);
                return null;
            }

            var tokenToUpdate = await _dbContext.AthleteTokens.FindAsync(token.AthleteId);
            if (tokenToUpdate == null)
            {
                _logger.LogError("Token to update not found for athlete {AthleteId}", token.AthleteId);
                return null;
            }

            // Update the token in the database
            tokenToUpdate.AccessToken = tokenResponse.Access_token;
            tokenToUpdate.RefreshToken = tokenResponse.Refresh_token;
            tokenToUpdate.ExpiresAt = DateTime.SpecifyKind(
                    DateTimeOffset.FromUnixTimeSeconds(tokenResponse.Expires_at).DateTime,
                    DateTimeKind.Utc);
            tokenToUpdate.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Successfully refreshed token for athlete {AthleteId}", token.AthleteId);
            return tokenToUpdate.AccessToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while refreshing token for athlete {AthleteId}", token.AthleteId);
            return null;

        }
    }
}
