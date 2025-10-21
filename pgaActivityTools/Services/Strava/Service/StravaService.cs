using System.Text.Json;
using pgaActivityTools.Models.Strava;
using pgaActivityTools.Services.Weather;

namespace pgaActivityTools.Services.Strava;

public class StravaService : IStravaService
{
    
    private readonly HttpClient _httpClient;
    private readonly IWeather _weatherService;
    private readonly ILogger<StravaService> _logger;

    public StravaService(HttpClient httpClient, IWeather weatherService, ILogger<StravaService> logger)
    {
        _httpClient = httpClient;
        _weatherService = weatherService;
        _logger = logger;
        _httpClient.BaseAddress = new Uri("https://www.strava.com/api/v3/");
    }

    public async Task<StravaActivity?> GetActivityByIdAsync(string accessToken, long activityId)
    {
        try
        {
            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.GetAsync($"activities/{activityId}");
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<StravaActivity>(
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching activity {ActivityId}", activityId);
            return null;
        }
    }
}