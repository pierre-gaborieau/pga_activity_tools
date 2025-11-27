namespace pgaActivityTools.Models.Weather;

public class WeatherData
{
    public string Description { get; set; } = string.Empty;
    public double Temperature { get; set; }
    public double FeelsLike { get; set; }
    public int Humidity { get; set; }
    public double WindSpeed { get; set; }
    public double WindAngle { get; set; }
    public int Cloudiness { get; set; }
    public string CityName { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}