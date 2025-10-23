using Microsoft.AspNetCore.Hosting;
using Moq;
using pgaActivityTools.Services.Version;
using pgaActivityTools.Services.Version.Service;
using Xunit;

namespace pgaActivityTools.Tests.Services;

public class VersionServiceTests
{
    [Fact]
    public void GetVersion_ReturnsNonEmptyString()
    {
        // Arrange
        var mockEnvironment = new Mock<IWebHostEnvironment>();
        mockEnvironment.Setup(e => e.EnvironmentName).Returns("Development");
        mockEnvironment.Setup(e => e.ContentRootPath).Returns("/app");
        
        var versionService = new VersionService(mockEnvironment.Object);

        // Act
        var version = versionService.GetVersionInfo();

        // Assert
        Assert.NotNull(version);
        Assert.NotEmpty(version.Version);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void GetVersion_WorksInDifferentEnvironments(string environmentName)
    {
        // Arrange
        var mockEnvironment = new Mock<IWebHostEnvironment>();
        mockEnvironment.Setup(e => e.EnvironmentName).Returns(environmentName);
        mockEnvironment.Setup(e => e.ContentRootPath).Returns("/app");
        
        var versionService = new VersionService(mockEnvironment.Object);

        // Act
        var version = versionService.GetVersionInfo();

        // Assert
        Assert.NotNull(version);
        Assert.NotEmpty(version.Version);
    }

    [Fact]
    public void GetVersion_ReturnsExpectedFormat()
    {
        // Arrange
        var mockEnvironment = new Mock<IWebHostEnvironment>();
        mockEnvironment.Setup(e => e.EnvironmentName).Returns("Production");
        mockEnvironment.Setup(e => e.ContentRootPath).Returns("/app");
        
        var versionService = new VersionService(mockEnvironment.Object);

        // Act
        var version = versionService.GetVersionInfo();

        // Assert
        // Vérifier le format de version (par exemple: "1.0.0" ou "1.0.0-dev")
        Assert.Matches(@"^\d+\.\d+\.\d+", version.Version);
    }
}