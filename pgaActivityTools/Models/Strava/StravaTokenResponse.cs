namespace pgaActivityTools.Models.Strava;

public record StravaTokenResponse(
    string Token_type,
    int Expires_at,
    int Expires_in,
    string Refresh_token,
    string Access_token,
    StravaAthlete Athlete
);