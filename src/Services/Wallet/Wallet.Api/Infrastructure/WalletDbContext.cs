using BuildingBlocks.ServiceDefaults.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Wallet.Api.Domain;

namespace Wallet.Api.Infrastructure;

public sealed class WalletDbContext(DbContextOptions<WalletDbContext> options) : DbContext(options)
{
    public DbSet<WalletEntry> Entries => Set<WalletEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WalletEntry>(e =>
        {
            e.ToTable("wallet_entries", t =>
            {
                t.HasCheckConstraint("ck_wallet_entries_amount", "amount > 0");
                t.HasCheckConstraint("ck_wallet_entries_status", "status IN ('Pending','Paid')");
                t.HasCheckConstraint("ck_wallet_entries_paid_at", "(status = 'Paid') = (paid_at IS NOT NULL)");
            });
            e.HasKey(x => x.Id);
            e.Property(x => x.UserExternalId).HasMaxLength(WalletEntry.UserIdMaxLength);
            e.Property(x => x.EventExternalId).HasMaxLength(WalletEntry.EventIdMaxLength);
            e.Property(x => x.Amount).HasPrecision(18, 4);
            e.Property(x => x.Status).HasMaxLength(16);
            e.HasIndex(x => x.CommissionId).IsUnique();
            e.HasIndex(x => new { x.CreatedAt, x.Id })
                .HasDatabaseName("ix_wallet_entries_pending")
                .HasFilter("status = 'Pending'");
            e.HasIndex(x => new { x.UserExternalId, x.PaidAt, x.Id })
                .HasDatabaseName("ix_wallet_entries_paid_by_user")
                .IsDescending(false, true, false)
                .IncludeProperties(x => x.Amount)
                .HasFilter("status = 'Paid'");
        });

        modelBuilder.AddTransactionalOutboxEntities();
    }
}

internal sealed class WalletDbContextFactory : IDesignTimeDbContextFactory<WalletDbContext>
{
    public WalletDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<WalletDbContext>()
        .UseNpgsql("Host=localhost;Database=wallet;Username=postgres;Password=postgres")
        .UseSnakeCaseNamingConvention()
        .Options);
}
