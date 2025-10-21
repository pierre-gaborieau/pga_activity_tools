namespace pgaActivityTools.Models.Database;

public class ProcessedActivity
{
    public long Id { get; set; } // Activity ID Strava
    public long AthleteId { get; set; }
    public DateTime ProcessedAt { get; set; }
    public string? WeatherDescription { get; set; }
    public double? Temperature { get; set; }
}