using FluentMigrator;

namespace pgaActivityTools.Migrations;

[Migration(20251021001)]
public class InitialCreate_20251021_001 : Migration
{
    public override void Up()
    {
        Create.Table("AthleteTokens")
              .WithColumn("AthleteId").AsInt64().PrimaryKey()
              .WithColumn("AccessToken").AsString(500).NotNullable()
              .WithColumn("RefreshToken").AsString(500).NotNullable()
              .WithColumn("ExpiresAt").AsDateTime().NotNullable()
              .WithColumn("CreatedAt").AsDateTime().NotNullable()
              .WithColumn("UpdatedAt").AsDateTime().NotNullable();

        // Table ProcessedActivities
        Create.Table("ProcessedActivities")
            .WithColumn("Id").AsInt64().PrimaryKey()
            .WithColumn("AthleteId").AsInt64().NotNullable()
            .WithColumn("ProcessedAt").AsDateTime().NotNullable()
            .WithColumn("WeatherDescription").AsString(255).Nullable()
            .WithColumn("Temperature").AsDouble().Nullable();

        // Index pour améliorer les performances
        Create.Index("IX_ProcessedActivities_AthleteId")
            .OnTable("ProcessedActivities")
            .OnColumn("AthleteId");

        Create.Index("IX_ProcessedActivities_ProcessedAt")
            .OnTable("ProcessedActivities")
            .OnColumn("ProcessedAt")
            .Descending();

        // Table AthleteWhitelist
        Create.Table("AthleteWhitelist")
            .WithColumn("AthleteId").AsInt64().PrimaryKey()
            .WithColumn("CreatedAt").AsDateTime().NotNullable().WithDefaultValue(SystemMethods.CurrentDateTime);

        // Seed data - Votre athlete ID
        Insert.IntoTable("AthleteWhitelist")
            .Row(new { AthleteId = 28759965, CreatedAt = DateTime.UtcNow });

    }

    public override void Down()
    {
        Delete.Table("AthleteWhitelist");
        Delete.Table("ProcessedActivities");
        Delete.Table("AthleteTokens");
    }
}
