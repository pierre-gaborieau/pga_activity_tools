namespace pgaActivityTools.Models.Strava;

public record StravaAthlete(
    long Id,
    string? Username,
    string? Firstname,
    string? Lastname
);