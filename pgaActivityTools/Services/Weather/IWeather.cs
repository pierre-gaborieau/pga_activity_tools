using pgaActivityTools.Models.Weather;

namespace pgaActivityTools.Services.Weather;

public interface IWeather
{
    Task<WeatherData?> GetWeatherAsync(double latitude, double longitude, DateTime date);
}
