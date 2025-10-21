using System.Reflection;
using pgaActivityTools.Models.Versions;

namespace pgaActivityTools.Services.Version.Service;

public class VersionService : IVersion
{
    private readonly IWebHostEnvironment _environment;
    private readonly VersionInfo _versionInfo;

    public VersionService(IWebHostEnvironment environment)
    {
        _environment = environment;
        _versionInfo = BuildVersionInfo();
    }


    private VersionInfo BuildVersionInfo()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                     ?? assembly.GetName().Version?.ToString()
                     ?? "unknown";

        var buildDate = GetBuildDate(assembly);

        return new VersionInfo
        {
            Version = version,
            Environment = _environment.EnvironmentName,
            BuildDate = buildDate
        };
    }

    private string GetBuildDate(Assembly assembly)
    {
        return new FileInfo(assembly.Location).LastWriteTimeUtc.ToString("yyyy-MM-ddTHH:mm:ssZ");
    }

    public VersionInfo GetVersionInfo()
    {
        return _versionInfo;
    }
}
