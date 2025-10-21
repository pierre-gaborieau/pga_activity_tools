using pgaActivityTools.Models.Versions;

namespace pgaActivityTools.Services.Version;

public interface IVersion
{
    VersionInfo GetVersionInfo();
}
