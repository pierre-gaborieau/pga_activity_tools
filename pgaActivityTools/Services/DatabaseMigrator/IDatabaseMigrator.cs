namespace pgaActivityTools.Services.DatabaseMigrator;

public interface IDatabaseMigrator
{
    void Migrate();
    void Rollback(long version);
}