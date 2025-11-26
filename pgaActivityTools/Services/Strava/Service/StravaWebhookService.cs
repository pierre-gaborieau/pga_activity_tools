using pgaActivityTools.Data;
using pgaActivityTools.Models.Database;
using pgaActivityTools.Models.Strava.Enum;
using pgaActivityTools.Models.Weather;
using pgaActivityTools.Models.Webhook;
using pgaActivityTools.Services.Weather;

namespace pgaActivityTools.Services.Strava.Service;

public class StravaWebhookService : IStravaWebhook
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly IStravaService _stravaService;
    private readonly IWeather _weatherService;
    private readonly ILogger<StravaWebhookService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IStravaTokenRefresher _tokenRefresher;
    public StravaWebhookService(
           HttpClient httpClient,
           IConfiguration configuration,
           IStravaService stravaService,
           ILogger<StravaWebhookService> logger,
           IWeather weatherService,
           IServiceScopeFactory scopeFactory,
           IStravaTokenRefresher tokenRefresher)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _stravaService = stravaService;
        _logger = logger;
        _tokenRefresher = tokenRefresher;
        _weatherService = weatherService;
        _httpClient.BaseAddress = new Uri("https://www.strava.com/api/v3/");
        _scopeFactory = scopeFactory;
    }

    public async Task<WebhookSubscription?> CreateSubscriptionAsync()
    {
        var clientId = _configuration["Strava:ClientId"];
        var clientSecret = _configuration["Strava:ClientSecret"];
        var callbackEndpoint = _configuration["Strava:WebhookCallbackEndpoint"];
        var callbackUrl = $"{_configuration["Application:BaseUrl"]}{callbackEndpoint}";
        var verifyToken = _configuration["Strava:WebhookVerifyToken"];

        _logger.LogInformation("Creating webhook subscription with:");
        _logger.LogInformation("  ClientId: {ClientId}", clientId);
        _logger.LogInformation("  CallbackUrl: {CallbackUrl}", callbackUrl);
        _logger.LogInformation("  VerifyToken: {VerifyToken}", verifyToken);

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
    {
        { "client_id", clientId ?? "" },
        { "client_secret", clientSecret ?? "" },
        { "callback_url", callbackUrl ?? "" },
        { "verify_token", verifyToken ?? "" }
    });

        try
        {
            var response = await _httpClient.PostAsync("push_subscriptions", content);

            var responseContent = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("Strava API response status: {StatusCode}", response.StatusCode);
            _logger.LogInformation("Strava API response body: {Body}", responseContent);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to create subscription. Status: {StatusCode}, Response: {Response}",
                    response.StatusCode, responseContent);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<WebhookSubscription>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating webhook subscription");
            return null;
        }
    }

    public async Task<List<WebhookSubscription>?> GetSubscriptionsAsync()
    {
        var clientId = _configuration["Strava:ClientId"];
        var clientSecret = _configuration["Strava:ClientSecret"];

        try
        {
            var response = await _httpClient.GetAsync(
                $"push_subscriptions?client_id={clientId}&client_secret={clientSecret}");

            var responseContent = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("Get subscriptions response: Status {StatusCode}, Body: {Body}",
                response.StatusCode, responseContent);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to get subscriptions. Status: {StatusCode}", response.StatusCode);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<List<WebhookSubscription>>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting webhook subscriptions");
            return null;
        }
    }

    public async Task<bool> DeleteSubscriptionAsync(int subscriptionId)
    {
        var clientId = _configuration["Strava:ClientId"];
        var clientSecret = _configuration["Strava:ClientSecret"];

        try
        {
            var response = await _httpClient.DeleteAsync(
                $"push_subscriptions/{subscriptionId}?client_id={clientId}&client_secret={clientSecret}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting webhook subscription");
            return false;
        }
    }

    public async Task ProcessActivityEventAsync(StravaWebhookEvent webhookEvent)
    {
        if (webhookEvent.Object_type != "activity")
        {
            _logger.LogInformation("Ignoring non-activity event");
            return;
        }

        var isCreate = webhookEvent.Aspect_type == "create";
        var isUpdate = webhookEvent.Aspect_type == "update";
        using var scope = _scopeFactory.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var alreadyProcessed = dbContext.ProcessedActivities.Any(a => a.Id == webhookEvent.Object_id);

        if (!isCreate && !isUpdate)
        {
            _logger.LogInformation("Ignoring event type: {AspectType}", webhookEvent.Aspect_type);
            return;
        }

        if (alreadyProcessed && isUpdate)
        {
            _logger.LogInformation("Activity {ActivityId} already processed, ignoring update", webhookEvent.Object_id);
            return;
        }

        _logger.LogInformation("Processing activity event: Type={Type}, ActivityId={ActivityId}, AthleteId={AthleteId}",
            webhookEvent.Aspect_type, webhookEvent.Object_id, webhookEvent.Owner_id);

        try
        {
            var token = dbContext.AthleteTokens
                .FirstOrDefault(t => t.AthleteId == webhookEvent.Owner_id);
            if (token == null)
            {
                _logger.LogWarning("No access token found for athlete {AthleteId}", webhookEvent.Owner_id);
                return;
            }

            _logger.LogInformation("Found access token for athlete {AthleteId}", webhookEvent.Owner_id);

            var validToken = await _tokenRefresher.GetValidAccessTokenAsync(webhookEvent.Owner_id);
            if (validToken == null)
            {
                _logger.LogWarning("Could not obtain valid access token for athlete {AthleteId}", webhookEvent.Owner_id);
                return;
            }
            var activity = await _stravaService.GetActivityByIdAsync(validToken, webhookEvent.Object_id);

            if (activity != null)
            {
                bool activityUpdated = false;
                string updatedDescription = activity.Description ?? "";
                string updatedTitle = activity.Name;
                WeatherData? weather = null;

                (updatedTitle, activityUpdated) = AddSportEmojiToTitle(activity.Name, activity.Sport_type);

                if (activity.Start_latlng != null && activity.Start_latlng.Length == 2)
                {
                    _logger.LogInformation("Activity details: {Name}, Lat: {Lat}, Lng: {Lng}",
                        activity.Name, activity.Start_latlng[0], activity.Start_latlng[1]);

                    // Récupérer la météo
                    weather = await _weatherService.GetWeatherAsync(
                         activity.Start_latlng[0],
                         activity.Start_latlng[1],
                         activity.Start_date);

                    if (weather != null)
                    {
                        _logger.LogInformation("Weather retrieved: {Description}, Temp: {Temp}°C",
                            weather.Description, weather.Temperature);

                        // Mettre à jour la description de l'activité avec la météo
                        updatedDescription = BuildDescriptionWithWeather(updatedDescription, weather);
                        updatedTitle = BuildTitleWithWeather(updatedTitle, weather, activity.Start_date.Hour >= 6 && activity.Start_date.Hour <= 18);
                        activityUpdated = true;
                    }
                    else
                    {
                        _logger.LogWarning("Could not retrieve weather for activity {ActivityId}", webhookEvent.Object_id);
                    }
                }
                else
                {
                    _logger.LogWarning("Activity {ActivityId} has no GPS coordinates", webhookEvent.Object_id);
                }

                if (activityUpdated)
                {
                    var success = await UpdateActivityAsync(
                            token.AccessToken,
                            webhookEvent.Object_id,
                            updatedTitle,
                            updatedDescription);

                    if (success)
                    {
                        _logger.LogInformation("✅ Activity {ActivityId} updated !", webhookEvent.Object_id);
                        var processedActivity = new ProcessedActivity
                        {
                            Id = webhookEvent.Object_id,

                            AthleteId = webhookEvent.Owner_id,
                            ProcessedAt = DateTime.UtcNow,
                            WeatherDescription = weather?.Description ?? "",
                            Temperature = weather?.Temperature ?? 0
                        };
                        dbContext.ProcessedActivities.Add(processedActivity);
                        await dbContext.SaveChangesAsync();
                    }
                    else
                    {
                        _logger.LogWarning("Failed to update activity description");
                    }
                }
            }
            else
            {
                _logger.LogWarning("Could not retrieve activity {ActivityId}", webhookEvent.Object_id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing activity event");
        }
    }

    private (string, bool) AddSportEmojiToTitle(string name, SportType sport_type)
    {
        if (Array.Exists([SportType.GravelRide, SportType.Ride], element => element == sport_type))
        {
            return ($"🚴 {name}", true);
        }
        else if (sport_type == SportType.VirtualRide)
        {
            return ($"🚴‍♂️🏠 {name}", true);
        }
        else if (Array.Exists([SportType.Run, SportType.TrailRun], element => element == sport_type))
        {
            return ($"🏃 {name}", true);
        }
        else if (sport_type == SportType.VirtualRun)
        {
            return ($"🏃‍♂️🏠 {name}", true);
        }
        else if (sport_type == SportType.Hike)
        {
            return ($"🥾 {name}", true);
        }
        else if (sport_type == SportType.MountainBikeRide)
        {
            return ($"🚵 {name}", true);
        }
        else if (sport_type == SportType.Walk)
        {
            return ($"🚶 {name}", true);
        }

        return (name, false);
    }

    private string BuildTitleWithWeather(string currentTitle, WeatherData weather, bool isDaytime)
    {
        return $"{emojiResolver(weather.Description, weather.Temperature, isDaytime)} {currentTitle}";
    }

    private string emojiResolver(string description, double temperature, bool isDaytime)
    {
        // Normaliser la description en minuscule
        var condition = description.ToLower();

        // Cas spéciaux indépendants du jour/nuit
        if (condition.Contains("thunder") || condition.Contains("orage")) return "⛈️";
        if (condition.Contains("snow") || condition.Contains("neige")) return "❄️";
        if (condition.Contains("drizzle") || condition.Contains("bruine")) return "🌦️";
        if (condition.Contains("mist") || condition.Contains("fog") || condition.Contains("brume") || condition.Contains("brouillard")) return "🌫️";

        if (isDaytime)
        {
            // Jour
            if (condition.Contains("rain") || condition.Contains("pluie")) return "🌧️";
            if (condition.Contains("cloud") || condition.Contains("nuage"))
            {
                if (condition.Contains("few") || condition.Contains("quelques") || condition.Contains("scattered") || condition.Contains("épars")) return "🌤️";
                if (condition.Contains("broken") || condition.Contains("fragmenté")) return "⛅";
                return "☁️";
            }
            if (condition.Contains("clear") || condition.Contains("dégagé") || condition.Contains("ensoleillé")) return "☀️";
            return "🌤️"; // Par défaut jour
        }
        else
        {
            // Nuit
            if (condition.Contains("rain") || condition.Contains("pluie")) return "🌧️";
            if (condition.Contains("cloud") || condition.Contains("nuage")) return "☁️";
            if (condition.Contains("clear") || condition.Contains("dégagé"))
            {
                if (temperature < 0) return "🌑"; // Nuit froide
                if (temperature < 10) return "🌙"; // Nuit fraîche
                return "🌕"; // Nuit claire et douce
            }
            if (temperature >= 0) return "🌗"; // Frais la nuit
            return "🌑"; // Froid la nuit
        }
    }

    private string BuildDescriptionWithWeather(string? currentDescription, WeatherData weather)
    {
        var weatherInfo = $@"🌡️ {weather.Temperature}°C (ressenti : {weather.FeelsLike}°C) ☁️ {weather.Description} 💨 Vent : {weather.WindSpeed} m/s du {buildWeatherDirection(weather.WindAngle)}";
        
        if (_configuration["Environment"] == "Development")
        {
            weatherInfo = $"[DEV MODE] {weatherInfo}";
        }

        if (!string.IsNullOrWhiteSpace(currentDescription))
        {
            return $"{currentDescription}\n\n{weatherInfo}";
        }

        return weatherInfo;
    }

    private string buildWeatherDirection(double windAngle)
    {
        if (windAngle >= 337.5 || windAngle < 22.5) return "N";
        if (windAngle >= 22.5 && windAngle < 67.5) return "NE";
        if (windAngle >= 67.5 && windAngle < 112.5) return "E";
        if (windAngle >= 112.5 && windAngle < 157.5) return "SE";
        if (windAngle >= 157.5 && windAngle < 202.5) return "S";
        if (windAngle >= 202.5 && windAngle < 247.5) return "SW";
        if (windAngle >= 247.5 && windAngle < 292.5) return "W";
        if (windAngle >= 292.5 && windAngle < 337.5) return "NW";
        return "N"; // Default
    }

    private async Task<bool> UpdateActivityAsync(string accessToken, long activityId, string title, string description)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Put, $"activities/{activityId}");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                {  "name", title },
                {  "description", description }
            });

            request.Content = content;

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to update activity. Status: {StatusCode}, Response: {Response}",
                    response.StatusCode, errorContent);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating activity description");
            return false;
        }
    }
}
