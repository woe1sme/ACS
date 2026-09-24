using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Partners.Api.Domain;

namespace Partners.Api.Infrastructure;

public sealed class PartnersDbContext(DbContextOptions<PartnersDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("ltree");

        modelBuilder.Entity<User>(user =>
        {
            user.ToTable("users");
            user.HasKey(u => u.ExternalId);
            user.Property(u => u.ExternalId).HasMaxLength(UserId.MaxLength);
            user.Property(u => u.ParentExternalId).HasMaxLength(UserId.MaxLength);
            user.Property(u => u.Path)
                .HasColumnType("ltree")
                .HasConversion(v => new LTree(v), v => v.ToString())
                .IsRequired();
            user.Ignore(u => u.TreePath);
            user.HasOne<User>().WithMany().HasForeignKey(u => u.ParentExternalId).OnDelete(DeleteBehavior.Restrict);
            user.HasIndex(u => u.ParentExternalId);
            user.HasIndex(u => u.Path).HasMethod("gist");
            user.ToTable(t => t.HasCheckConstraint("ck_users_external_id_format", "external_id ~ '^[A-Za-z0-9_]{1,64}$'"));
        });
    }
}

internal sealed class PartnersDbContextFactory : IDesignTimeDbContextFactory<PartnersDbContext>
{
    public PartnersDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<PartnersDbContext>()
        .UseNpgsql("Host=localhost;Database=partners;Username=postgres;Password=postgres")
        .UseSnakeCaseNamingConvention()
        .Options);
}
