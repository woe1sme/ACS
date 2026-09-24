using System.ComponentModel.DataAnnotations;
using System.Data.Common;
using BuildingBlocks.ServiceDefaults.Configuration;
using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace BuildingBlocks.ServiceDefaults.Persistence;

public sealed class DatabaseOptions
{
    [Range(1, 600)]
    public int CommandTimeoutSeconds { get; set; } = 30;
}

public static class PersistenceExtensions
{
    /// <summary>Registers a Postgres-backed DbContext (snake_case naming) and its readiness check.</summary>
    public static WebApplicationBuilder AddServiceDbContext<TContext>(
        this WebApplicationBuilder builder,
        string connectionStringName,
        Action<NpgsqlDbContextOptionsBuilder>? configureNpgsql = null)
        where TContext : DbContext
    {
        var connectionString = builder.Configuration.GetConnectionString(connectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException($"Connection string '{connectionStringName}' is not configured.");
        }

        builder.Services.AddValidatedOptions<DatabaseOptions>(builder.Configuration, "Database");
        var timeout = builder.Configuration.GetSection("Database").GetValue<int?>("CommandTimeoutSeconds") ?? 30;

        builder.Services.AddDbContext<TContext>(options => options
            .UseNpgsql(connectionString, npgsql =>
            {
                npgsql.CommandTimeout(timeout);
                configureNpgsql?.Invoke(npgsql);
            })
            .UseSnakeCaseNamingConvention());

        builder.Services.AddHealthChecks().AddCheck<DbContextHealthCheck<TContext>>("database", tags: [Extensions.ReadyTag]);
        return builder;
    }

    /// <summary>Adds MassTransit inbox/outbox tables to the model.</summary>
    public static ModelBuilder AddTransactionalOutboxEntities(this ModelBuilder modelBuilder)
    {
        modelBuilder.AddInboxStateEntity();
        modelBuilder.AddOutboxMessageEntity();
        modelBuilder.AddOutboxStateEntity();
        return modelBuilder;
    }

    /// <summary>Applies pending migrations under a Postgres advisory lock so that concurrent replicas do not race.</summary>
    public static async Task ApplyMigrationsAsync<TContext>(this WebApplication app, CancellationToken cancellationToken = default)
        where TContext : DbContext
    {
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Migrations");
        var lockKey = StableHash(typeof(TContext).FullName!);
        const int maxAttempts = 10;

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var scope = app.Services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<TContext>();
                await db.Database.OpenConnectionAsync(cancellationToken);
                try
                {
                    await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_lock({0})", [lockKey], cancellationToken);
                    try
                    {
                        await db.Database.MigrateAsync(cancellationToken);
                    }
                    finally
                    {
                        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_unlock({0})", [lockKey], cancellationToken);
                    }

                    if (db.Database.GetDbConnection() is NpgsqlConnection connection)
                    {
                        await connection.ReloadTypesAsync();
                    }
                }
                finally
                {
                    await db.Database.CloseConnectionAsync();
                }

                NpgsqlConnection.ClearAllPools();
                logger.LogInformation("Database migrations for {Context} are applied", typeof(TContext).Name);
                return;
            }
            catch (Exception ex) when (ex is DbException or TimeoutException && attempt < maxAttempts)
            {
                logger.LogWarning(ex, "Database is not reachable yet (attempt {Attempt}/{Max})", attempt, maxAttempts);
                await Task.Delay(TimeSpan.FromSeconds(Math.Min(2 * attempt, 10)), cancellationToken);
            }
        }
    }

    private static long StableHash(string value)
    {
        unchecked
        {
            var hash = 1469598103934665603L;
            foreach (var c in value)
            {
                hash = (hash ^ c) * 1099511628211L;
            }

            return hash;
        }
    }
}
