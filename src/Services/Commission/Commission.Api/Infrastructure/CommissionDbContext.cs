using BuildingBlocks.ServiceDefaults.Persistence;
using Commission.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Commission.Api.Infrastructure;

public sealed class CommissionDbContext(DbContextOptions<CommissionDbContext> options) : DbContext(options)
{
    public DbSet<CommissionRecord> Commissions => Set<CommissionRecord>();

    public DbSet<SchemeSettings> SchemeSettings => Set<SchemeSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CommissionRecord>(c =>
        {
            c.ToTable("commissions", t =>
            {
                t.HasCheckConstraint("ck_commissions_level", "level >= 1");
                t.HasCheckConstraint("ck_commissions_amount", "amount > 0");
                t.HasCheckConstraint("ck_commissions_paid_at", "(is_paid AND paid_at IS NOT NULL) OR (NOT is_paid AND paid_at IS NULL)");
            });
            c.HasKey(x => x.Id);
            c.Property(x => x.EventExternalId).HasMaxLength(128);
            c.Property(x => x.BeneficiaryExternalId).HasMaxLength(64);
            c.Property(x => x.Amount).HasPrecision(18, 4);
            c.Property(x => x.SchemeType).HasMaxLength(32);
            c.HasIndex(x => new { x.EventExternalId, x.BeneficiaryExternalId }).IsUnique();
        });

        modelBuilder.Entity<SchemeSettings>(s =>
        {
            s.ToTable("commission_scheme_settings", t => t.HasCheckConstraint("ck_scheme_settings_singleton", "id = 1"));
            s.HasKey(x => x.Id);
            s.Property(x => x.Id).ValueGeneratedNever();
            s.Property(x => x.ActiveSchemeType).HasMaxLength(32);
            s.HasData(new
            {
                Id = Domain.SchemeSettings.SingletonId,
                ActiveSchemeType = LinearScheme.Name,
                UpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            });
        });

        modelBuilder.AddTransactionalOutboxEntities();
    }
}

internal sealed class CommissionDbContextFactory : IDesignTimeDbContextFactory<CommissionDbContext>
{
    public CommissionDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<CommissionDbContext>()
        .UseNpgsql("Host=localhost;Database=commission;Username=postgres;Password=postgres")
        .UseSnakeCaseNamingConvention()
        .Options);
}
