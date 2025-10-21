using Microsoft.EntityFrameworkCore;
using pgaActivityTools.Models.Database;

namespace pgaActivityTools.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<ProcessedActivity> ProcessedActivities { get; set; }
    public DbSet<AthleteToken> AthleteTokens { get; set; }
    public DbSet<AthleteWhitelist> AthleteWhitelist { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ProcessedActivity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.AthleteId);
            entity.HasIndex(e => e.ProcessedAt);
        });

        modelBuilder.Entity<AthleteToken>(entity =>
        {
            entity.HasKey(e => e.AthleteId);
        });

        modelBuilder.Entity<AthleteWhitelist>(entity =>
        {
            entity.HasKey(e => e.AthleteId);
        });
    }
}