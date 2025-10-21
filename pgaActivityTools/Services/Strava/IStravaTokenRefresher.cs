namespace pgaActivityTools.Services.Strava;

public interface IStravaTokenRefresher
{
    Task<string?> GetValidAccessTokenAsync(long athleteId);
}
