using Partners.Api.Domain;

namespace Partners.Api.Application;

public sealed record DescendantRow(string ExternalId, string? ParentExternalId, int Level);

public interface IUserRepository
{
    Task<User?> FindAsync(string externalId, CancellationToken ct);

    /// <summary>Inserts the user; returns false when a user with the same id already exists.</summary>
    Task<bool> TryAddAsync(User user, CancellationToken ct);

    /// <summary>Re-roots the user's subtree under the new parent and updates the direct link.</summary>
    Task MoveSubtreeAsync(User user, User newParent, CancellationToken ct);

    Task<(IReadOnlyList<DescendantRow> Items, long Total)> GetDescendantsAsync(User user, int skip, int take, CancellationToken ct);
}

/// <summary>Serializes path-dependent tree mutations (create with parent, change parent) in one transaction.</summary>
public interface ITreeMutationScope
{
    Task<ITreeMutation> BeginAsync(CancellationToken ct);
}

public interface ITreeMutation : IAsyncDisposable
{
    Task CommitAsync(CancellationToken ct);
}
