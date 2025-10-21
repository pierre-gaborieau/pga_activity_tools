using pgaActivityTools.Models.Strava;

namespace pgaActivityTools.Services.Strava;

public interface IStravaService
{
    Task<StravaActivity?> GetActivityByIdAsync(string accessToken, long activityId);
}
