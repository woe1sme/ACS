namespace Commission.Api.Domain;

/// <summary>Commission scheme (Strategy). A new scheme is a new implementation plus one DI registration.</summary>
public interface ICommissionScheme
{
    /// <summary>Stable name stored in every commission as its scheme type.</summary>
    string Type { get; }

    /// <summary>Multiplier for the given level (1 = direct partner); amount = coefficient × profit / 100.</summary>
    decimal Coefficient(int level);
}

public sealed class LinearScheme : ICommissionScheme
{
    public const string Name = "Linear";

    public string Type => Name;

    public decimal Coefficient(int level)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(level, 1);
        return level;
    }
}

public sealed class FibonacciScheme : ICommissionScheme
{
    public const string Name = "Fibonacci";

    public string Type => Name;

    /// <summary>F(1) = 1, F(2) = 1, F(3) = 2, F(4) = 3, F(5) = 5, ...</summary>
    public decimal Coefficient(int level)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(level, 1);
        decimal previous = 0, current = 1;
        for (var i = 1; i < level; i++)
        {
            var next = checked(previous + current);
            previous = current;
            current = next;
        }

        return current;
    }
}

public sealed class SchemeRegistry
{
    private readonly Dictionary<string, ICommissionScheme> _schemes;

    public SchemeRegistry(IEnumerable<ICommissionScheme> schemes) =>
        _schemes = schemes.ToDictionary(s => s.Type, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<string> Types => _schemes.Values.Select(s => s.Type).Order().ToList();

    public bool TryGet(string type, out ICommissionScheme scheme) => _schemes.TryGetValue(type, out scheme!);

    public ICommissionScheme Get(string type) =>
        TryGet(type, out var scheme) ? scheme : throw new InvalidOperationException($"Commission scheme '{type}' is not registered.");
}
