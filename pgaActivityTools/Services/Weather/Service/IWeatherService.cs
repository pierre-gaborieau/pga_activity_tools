using pgaActivityTools.Models.Weather;

namespace pgaActivityTools.Services.Weather.Service;

public class WeatherService : IWeather
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<WeatherService> _logger;

    public WeatherService(HttpClient httpClient, IConfiguration configuration, ILogger<WeatherService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<WeatherData?> GetWeatherAsync(double latitude, double longitude, DateTime timestamp)
    {
        try
        {
            var apiKey = _configuration["OpenWeatherMap:ApiKey"];
            var url = $"https://api.openweathermap.org/data/2.5/weather?lat={latitude}&lon={longitude}&appid={apiKey}&units=metric&lang=fr";

            _logger.LogInformation("Fetching weather for Lat: {Lat}, Lon: {Lon}", latitude, longitude);

            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Weather API error: {StatusCode}, {Content}", response.StatusCode, errorContent);
                return null;
            }

            var weatherResponse = await response.Content.ReadFromJsonAsync<OpenWeatherMapResponse>();

            if (weatherResponse?.Weather == null || weatherResponse.Weather.Length == 0)
            {
                _logger.LogWarning("No weather data returned");
                return null;
            }

            var weatherData = new WeatherData
            {
                Description = weatherResponse.Weather[0].Description ?? "N/A",
                Temperature = Math.Round(weatherResponse.Main?.Temp ?? 0, 1),
                FeelsLike = Math.Round(weatherResponse.Main?.Feels_like ?? 0, 1),
                Humidity = weatherResponse.Main?.Humidity ?? 0,
                WindSpeed = Math.Round(weatherResponse.Wind?.Speed ?? 0, 1),
                WindAngle = weatherResponse.Wind?.Deg ?? 0,
                Cloudiness = weatherResponse.Clouds?.All ?? 0,
                CityName = weatherResponse.Name ?? "Unknown",
                Timestamp = timestamp
            };

            _logger.LogInformation("Weather retrieved: {Description}, {Temp}°C", weatherData.Description, weatherData.Temperature);

            return weatherData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching weather data");
            return null;
        }
    }
}

// Modèles pour la réponse OpenWeatherMap
public class OpenWeatherMapResponse
{
    public WeatherDescription[]? Weather { get; set; }
    public MainWeather? Main { get; set; }
    public Wind? Wind { get; set; }
    public Clouds? Clouds { get; set; }
    public string? Name { get; set; }
}

public class WeatherDescription
{
    public string? Description { get; set; }
}

public class MainWeather
{
    public double Temp { get; set; }
    public double Feels_like { get; set; }
    public int Humidity { get; set; }
}

public class Wind
{
    public double Speed { get; set; }
    public double Deg { get; set; }
}

public class Clouds
{
    public int All { get; set; }
}