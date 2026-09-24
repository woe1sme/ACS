using Commission.Api.Domain;

namespace Commission.Api.Application;

/// <summary>Chain of partners above a user (level 1 = direct partner).</summary>
public interface IAncestorsProvider
{
    Task<IReadOnlyList<ChainMember>> GetAncestorsAsync(string userExternalId, CancellationToken ct);
}
