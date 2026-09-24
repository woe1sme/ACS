using System.Text.RegularExpressions;
using BuildingBlocks.ServiceDefaults.Errors;

namespace Partners.Api.Domain;

/// <summary>External user identifier. It is also used as an ltree label, hence the restricted alphabet.</summary>
public readonly partial record struct UserId
{
    public const int MaxLength = 64;

    private UserId(string value) => Value = value;

    public string Value { get; }

    public static UserId Create(string? value, string field = "externalId")
    {
        if (string.IsNullOrEmpty(value) || !Format().IsMatch(value))
        {
            throw new ValidationException($"{field} must match ^[A-Za-z0-9_]{{1,{MaxLength}}}$.");
        }

        return new UserId(value);
    }

    public override string ToString() => Value;

    [GeneratedRegex("^[A-Za-z0-9_]{1,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Format();
}
