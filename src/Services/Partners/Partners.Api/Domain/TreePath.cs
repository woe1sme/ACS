namespace Partners.Api.Domain;

public sealed record Ancestor(string ExternalId, int Level);

/// <summary>Materialized path of a user in the partner tree: labels from the root down to the user, joined by '.'.</summary>
public sealed record TreePath
{
    private readonly string[] _labels;

    private TreePath(string[] labels) => _labels = labels;

    public string Value => string.Join('.', _labels);

    public int Depth => _labels.Length;

    public string Owner => _labels[^1];

    public static TreePath Root(UserId user) => new([user.Value]);

    public static TreePath Parse(string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);
        return new TreePath(value.Split('.'));
    }

    public TreePath Child(UserId user) => new([.. _labels, user.Value]);

    /// <summary>Ancestors ordered from the direct parent (level 1) up to the root.</summary>
    public IReadOnlyList<Ancestor> Ancestors()
    {
        var result = new List<Ancestor>(_labels.Length - 1);
        for (var i = _labels.Length - 2; i >= 0; i--)
        {
            result.Add(new Ancestor(_labels[i], _labels.Length - 1 - i));
        }

        return result;
    }

    public bool IsSelfOrDescendantOf(TreePath other) =>
        other._labels.Length <= _labels.Length && other._labels.AsSpan().SequenceEqual(_labels.AsSpan(0, other._labels.Length));

    public override string ToString() => Value;
}
