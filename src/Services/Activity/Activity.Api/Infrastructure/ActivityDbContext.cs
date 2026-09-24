using Activity.Api.Domain;
using BuildingBlocks.ServiceDefaults.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Activity.Api.Infrastructure;

public sealed class ActivityDbContext(DbContextOptions<ActivityDbContext> options) : DbContext(options)
{
    public DbSet<ProfitEvent> Events => Set<ProfitEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProfitEvent>(e =>
        {
            e.ToTable("profit_events");
            e.HasKey(x => x.ExternalEventId);
            e.Property(x => x.ExternalEventId).HasMaxLength(ProfitEvent.EventIdMaxLength);
            e.Property(x => x.UserExternalId).HasMaxLength(ProfitEvent.UserIdMaxLength);
            e.Property(x => x.Profit).HasPrecision(18, 4);
            e.Ignore(x => x.IsPositive);
            e.HasIndex(x => new { x.UserExternalId, x.ReceivedAt, x.ExternalEventId }).IsDescending(false, true, false);
        });

        modelBuilder.AddTransactionalOutboxEntities();
    }
}

internal sealed class ActivityDbContextFactory : IDesignTimeDbContextFactory<ActivityDbContext>
{
    public ActivityDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<ActivityDbContext>()
        .UseNpgsql("Host=localhost;Database=activity;Username=postgres;Password=postgres")
        .UseSnakeCaseNamingConvention()
        .Options);
}
