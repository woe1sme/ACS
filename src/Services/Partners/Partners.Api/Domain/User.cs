namespace Partners.Api.Domain;

public sealed class User
{
    private User()
    {
    }

    public string ExternalId { get; private set; } = null!;

    public string? ParentExternalId { get; private set; }

    public string Path { get; private set; } = null!;

    public TreePath TreePath => TreePath.Parse(Path);

    public static User CreateRoot(UserId id) => new() { ExternalId = id.Value, Path = TreePath.Root(id).Value };

    public static User CreateChild(UserId id, User parent) => new()
    {
        ExternalId = id.Value,
        ParentExternalId = parent.ExternalId,
        Path = parent.TreePath.Child(id).Value,
    };
}
