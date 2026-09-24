using BuildingBlocks.ServiceDefaults.Errors;
using BuildingBlocks.ServiceDefaults.Paging;
using Partners.Api.Domain;

namespace Partners.Api.Application;

public sealed record UserDto(string ExternalId, string? ParentExternalId);

public sealed record CreateUserResult(UserDto User, bool Created);

public sealed class PartnersService(IUserRepository users, ITreeMutationScope mutations)
{
    public async Task<CreateUserResult> CreateUserAsync(string? externalId, string? parentExternalId, CancellationToken ct)
    {
        var id = UserId.Create(externalId);
        UserId? parentId = parentExternalId is null ? null : UserId.Create(parentExternalId, "parentExternalId");
        if (parentId == id)
        {
            throw new ValidationException("A user cannot be its own partner.");
        }

        var existing = await users.FindAsync(id.Value, ct);
        if (existing is not null)
        {
            return ResolveExisting(existing, parentId);
        }

        bool added;
        if (parentId is null)
        {
            added = await users.TryAddAsync(User.CreateRoot(id), ct);
        }
        else
        {
            await using var mutation = await mutations.BeginAsync(ct);
            var parent = await users.FindAsync(parentId.Value.Value, ct)
                ?? throw new ValidationException($"Parent user '{parentId.Value}' does not exist.");
            added = await users.TryAddAsync(User.CreateChild(id, parent), ct);
            if (added)
            {
                await mutation.CommitAsync(ct);
            }
        }

        if (added)
        {
            return new CreateUserResult(new UserDto(id.Value, parentId?.Value), Created: true);
        }

        var raced = await users.FindAsync(id.Value, ct)
            ?? throw new InvalidOperationException("User insert conflicted but the user was not found.");
        return ResolveExisting(raced, parentId);
    }

    public async Task<UserDto> ChangeParentAsync(string? externalId, string? parentExternalId, CancellationToken ct)
    {
        var id = UserId.Create(externalId);
        if (parentExternalId is null)
        {
            throw new ValidationException("parentExternalId is required.");
        }

        var parentId = UserId.Create(parentExternalId, "parentExternalId");

        await using var mutation = await mutations.BeginAsync(ct);
        var user = await users.FindAsync(id.Value, ct) ?? throw new NotFoundException($"User '{id}' was not found.");
        var parent = await users.FindAsync(parentId.Value, ct)
            ?? throw new ValidationException($"Parent user '{parentId}' does not exist.");

        if (user.ParentExternalId == parent.ExternalId)
        {
            return new UserDto(user.ExternalId, user.ParentExternalId);
        }

        if (parent.TreePath.IsSelfOrDescendantOf(user.TreePath))
        {
            throw new ConflictException("The new parent is the user itself or one of its descendants; cycles are not allowed.");
        }

        await users.MoveSubtreeAsync(user, parent, ct);
        await mutation.CommitAsync(ct);
        return new UserDto(user.ExternalId, parent.ExternalId);
    }

    public async Task<IReadOnlyList<Ancestor>> GetAncestorsAsync(string? externalId, CancellationToken ct)
    {
        var id = UserId.Create(externalId);
        var user = await users.FindAsync(id.Value, ct) ?? throw new NotFoundException($"User '{id}' was not found.");
        return user.TreePath.Ancestors();
    }

    public async Task<PagedResponse<Ancestor>> GetAncestorsPageAsync(string? externalId, PageRequest page, CancellationToken ct)
    {
        var all = await GetAncestorsAsync(externalId, ct);
        return new PagedResponse<Ancestor>(all.Skip(page.Skip).Take(page.PageSize).ToList(), page.Page, page.PageSize, all.Count);
    }

    public async Task<PagedResponse<DescendantRow>> GetDescendantsPageAsync(string? externalId, PageRequest page, CancellationToken ct)
    {
        var id = UserId.Create(externalId);
        var user = await users.FindAsync(id.Value, ct) ?? throw new NotFoundException($"User '{id}' was not found.");
        var (items, total) = await users.GetDescendantsAsync(user, page.Skip, page.PageSize, ct);
        return new PagedResponse<DescendantRow>(items, page.Page, page.PageSize, total);
    }

    private static CreateUserResult ResolveExisting(User existing, UserId? requestedParent)
    {
        if (existing.ParentExternalId == requestedParent?.Value)
        {
            return new CreateUserResult(new UserDto(existing.ExternalId, existing.ParentExternalId), Created: false);
        }

        throw new ConflictException(
            $"User '{existing.ExternalId}' already exists with a different parent; use PUT /users/{{externalId}}/parent to change it.");
    }
}
