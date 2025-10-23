using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Moq;
using pgaActivityTools.Data;
using pgaActivityTools.Endpoints;
using pgaActivityTools.Models.Versions;
using pgaActivityTools.Services.Version;

namespace pgaActivityTools.Tests.Endpoints;

public class SystemEndpointsTests
{
    private readonly ApplicationDbContext _dbContext;
    private readonly Mock<IVersion> _mockVersion;

    public SystemEndpointsTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
           .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
           .Options;

        _dbContext = new ApplicationDbContext(options);

        _mockVersion = new Mock<IVersion>();
        _mockVersion.Setup(v => v.GetVersionInfo())
            .Returns(new VersionInfo
            {
                Version = "1.0.0-test",
                Environment = "Test"
            });
    }

    [Fact]
    public async Task GetVersionInfo()
    {
        var result = SystemEndpoints.GetVersion(_mockVersion.Object);
        Assert.IsType<Ok<VersionInfo>>(result);
    }
}