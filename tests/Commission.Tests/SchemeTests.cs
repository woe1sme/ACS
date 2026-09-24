using Commission.Api.Domain;

namespace Commission.Tests;

public class SchemeTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    [InlineData(10, 10)]
    public void Linear_coefficient_equals_level(int level, int expected) =>
        new LinearScheme().Coefficient(level).Should().Be(expected);

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(3, 2)]
    [InlineData(4, 3)]
    [InlineData(5, 5)]
    [InlineData(6, 8)]
    [InlineData(7, 13)]
    [InlineData(10, 55)]
    public void Fibonacci_coefficient_follows_sequence(int level, int expected) =>
        new FibonacciScheme().Coefficient(level).Should().Be(expected);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Levels_start_at_one(int level)
    {
        FluentActions.Invoking(() => new LinearScheme().Coefficient(level)).Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => new FibonacciScheme().Coefficient(level)).Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Registry_resolves_case_insensitively_and_lists_types()
    {
        var registry = new SchemeRegistry([new LinearScheme(), new FibonacciScheme()]);

        registry.Get("fibonacci").Should().BeOfType<FibonacciScheme>();
        registry.Types.Should().Equal("Fibonacci", "Linear");
        registry.TryGet("Quadratic", out _).Should().BeFalse();
    }

    [Fact]
    public void A_new_scheme_plugs_in_without_touching_existing_ones()
    {
        var registry = new SchemeRegistry([new LinearScheme(), new FibonacciScheme(), new FlatScheme()]);

        var result = CommissionCalculator.Calculate([new("p1", 1), new("p2", 2)], 100m, registry.Get("Flat"));

        result.Select(r => r.Amount).Should().Equal(7m, 7m);
        result.Should().OnlyContain(r => r.SchemeType == "Flat");
    }

    private sealed class FlatScheme : ICommissionScheme
    {
        public string Type => "Flat";

        public decimal Coefficient(int level) => 7;
    }
}
