using BuildingBlocks.ServiceDefaults.Errors;
using Partners.Api.Application;
using Partners.Api.Domain;

namespace Partners.Tests;

public class PartnersServiceTests
{
    private readonly FakeUsers _users = new();
    private readonly PartnersService _service;

    public PartnersServiceTests() => _service = new PartnersService(_users, new NoopMutations());

    [Fact]
    public async Task Creates_root_and_child()
    {
        (await _service.CreateUserAsync("root", null, default)).Created.Should().BeTrue();
        var child = await _service.CreateUserAsync("a", "root", default);

        child.Created.Should().BeTrue();
        _users.Store["a"].Path.Should().Be("root.a");
    }

    [Fact]
    public async Task Repeated_create_with_same_parent_is_idempotent()
    {
        await _service.CreateUserAsync("root", null, default);
        await _service.CreateUserAsync("a", "root", default);

        var again = await _service.CreateUserAsync("a", "root", default);

        again.Created.Should().BeFalse();
    }

    [Fact]
    public async Task Create_with_different_parent_conflicts()
    {
        await _service.CreateUserAsync("root", null, default);
        await _service.CreateUserAsync("other", null, default);
        await _service.CreateUserAsync("a", "root", default);

        await FluentActions.Awaiting(() => _service.CreateUserAsync("a", "other", default))
            .Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Create_with_missing_parent_is_rejected() =>
        await FluentActions.Awaiting(() => _service.CreateUserAsync("a", "ghost", default))
            .Should().ThrowAsync<ValidationException>();

    [Fact]
    public async Task Change_parent_to_descendant_is_a_cycle()
    {
        await _service.CreateUserAsync("root", null, default);
        await _service.CreateUserAsync("a", "root", default);
        await _service.CreateUserAsync("b", "a", default);

        await FluentActions.Awaiting(() => _service.ChangeParentAsync("a", "b", default))
            .Should().ThrowAsync<ConflictException>();
        await FluentActions.Awaiting(() => _service.ChangeParentAsync("a", "a", default))
            .Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Change_parent_moves_subtree()
    {
        await _service.CreateUserAsync("root", null, default);
        await _service.CreateUserAsync("r2", null, default);
        await _service.CreateUserAsync("a", "root", default);
        await _service.CreateUserAsync("b", "a", default);

        await _service.ChangeParentAsync("a", "r2", default);

        (await _service.GetAncestorsAsync("b", default)).Should().Equal(new Ancestor("a", 1), new Ancestor("r2", 2));
    }

    private sealed class FakeUsers : IUserRepository
    {
        public Dictionary<string, User> Store { get; } = [];

        public Task<User?> FindAsync(string externalId, CancellationToken ct) => Task.FromResult(Store.GetValueOrDefault(externalId));

        public Task<bool> TryAddAsync(User user, CancellationToken ct) => Task.FromResult(Store.TryAdd(user.ExternalId, user));

        public Task MoveSubtreeAsync(User user, User newParent, CancellationToken ct)
        {
            var prefix = user.Path;
            var newPrefix = newParent.TreePath.Child(UserId.Create(user.ExternalId)).Value;
            foreach (var u in Store.Values.Where(u => TreePath.Parse(u.Path).IsSelfOrDescendantOf(user.TreePath)).ToList())
            {
                var path = newPrefix + u.Path[prefix.Length..];
                var parent = u.ExternalId == user.ExternalId ? newParent.ExternalId : u.ParentExternalId;
                Store[u.ExternalId] = Rebuild(u.ExternalId, parent, path);
            }

            return Task.CompletedTask;
        }

        public Task<(IReadOnlyList<DescendantRow> Items, long Total)> GetDescendantsAsync(User user, int skip, int take, CancellationToken ct) =>
            throw new NotSupportedException();

        private static User Rebuild(string id, string? parent, string path)
        {
            var labels = path.Split('.');
            var user = User.CreateRoot(UserId.Create(labels[0]));
            for (var i = 1; i < labels.Length; i++)
            {
                user = User.CreateChild(UserId.Create(labels[i]), user);
            }

            return user.ExternalId == id && user.ParentExternalId == parent ? user : throw new InvalidOperationException();
        }
    }

    private sealed class NoopMutations : ITreeMutationScope
    {
        public Task<ITreeMutation> BeginAsync(CancellationToken ct) => Task.FromResult<ITreeMutation>(new Noop());

        private sealed class Noop : ITreeMutation
        {
            public Task CommitAsync(CancellationToken ct) => Task.CompletedTask;

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
