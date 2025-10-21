using FluentMigrator.Runner;

namespace pgaActivityTools.Services.DatabaseMigrator.Service;

public class DatabaseMigratorService : IDatabaseMigrator
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DatabaseMigratorService> _logger;

    public DatabaseMigratorService(IServiceProvider serviceProvider, ILogger<DatabaseMigratorService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public void Migrate()
    {
        using var scope = _serviceProvider.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();

        try
        {
            _logger.LogInformation("Starting database migration...");
            runner.MigrateUp();
            _logger.LogInformation("Database migration completed successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred during database migration.");
        }
    }

    public void Rollback(long version)
    {
        using var scope = _serviceProvider.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();

        try
        {
            _logger.LogInformation($"Starting database rollback to version {version}...");
            runner.MigrateDown(version);
            _logger.LogInformation("Database rollback completed successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred during database rollback.");
        }
    }
}