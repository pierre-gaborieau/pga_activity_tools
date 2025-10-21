namespace pgaActivityTools.Models.Strava;

public class StravaActivity
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public DateTime Start_date { get; set; }
    public double[]? Start_latlng { get; set; }
    public string? Description { get; set; }
    public double Distance { get; set; }
    public int Moving_time { get; set; }
    public int Elapsed_time { get; set; }
}