using Microsoft.EntityFrameworkCore;
using Npgsql;
using Partners.Api.Application;
using Partners.Api.Domain;

namespace Partners.Api.Infrastructure;

internal sealed class UserRepository(PartnersDbContext db) : IUserRepository
{
    public Task<User?> FindAsync(string externalId, CancellationToken ct) =>
        db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.ExternalId == externalId, ct);

    public async Task<bool> TryAddAsync(User user, CancellationToken ct)
    {
        db.Users.Add(user);
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            db.ChangeTracker.Clear();
            return false;
        }
    }

    public async Task MoveSubtreeAsync(User user, User newParent, CancellationToken ct)
    {
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE users
            SET path = {newParent.Path}::ltree || subpath(path, nlevel({user.Path}::ltree) - 1)
            WHERE path <@ {user.Path}::ltree
            """,
            ct);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE users SET parent_external_id = {newParent.ExternalId} WHERE external_id = {user.ExternalId}",
            ct);
    }

    public async Task<(IReadOnlyList<DescendantRow> Items, long Total)> GetDescendantsAsync(User user, int skip, int take, CancellationToken ct)
    {
        var baseLevel = user.TreePath.Depth;
        var total = await db.Database
            .SqlQuery<long>($"SELECT count(*) AS \"Value\" FROM users WHERE path <@ {user.Path}::ltree AND external_id <> {user.ExternalId}")
            .SingleAsync(ct);

        var items = await db.Database
            .SqlQuery<DescendantRow>(
                $"""
                SELECT external_id, parent_external_id, nlevel(path) - {baseLevel} AS level
                FROM users
                WHERE path <@ {user.Path}::ltree AND external_id <> {user.ExternalId}
                ORDER BY nlevel(path), external_id
                OFFSET {skip} LIMIT {take}
                """)
            .ToListAsync(ct);

        return (items, total);
    }
}

internal sealed class TreeMutationScope(PartnersDbContext db) : ITreeMutationScope
{
    private const long TreeLockKey = 7_342_001;

    public async Task<ITreeMutation> BeginAsync(CancellationToken ct)
    {
        var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({TreeLockKey})", ct);
        return new Mutation(transaction);
    }

    private sealed class Mutation(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction) : ITreeMutation
    {
        public Task CommitAsync(CancellationToken ct) => transaction.CommitAsync(ct);

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
