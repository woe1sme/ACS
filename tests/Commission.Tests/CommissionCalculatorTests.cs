using Commission.Api.Domain;

namespace Commission.Tests;

public class CommissionCalculatorTests
{
    private static readonly ChainMember[] Chain =
    [
        new("p1", 1),
        new("p2", 2),
        new("p3", 3),
        new("p4", 4),
        new("p5", 5),
    ];

    [Fact]
    public void Linear_scheme_charges_level_percent_of_profit()
    {
        var result = CommissionCalculator.Calculate(Chain, 200m, new LinearScheme());

        result.Select(r => (r.BeneficiaryExternalId, r.Level, r.Amount)).Should().Equal(
            ("p1", 1, 2m),
            ("p2", 2, 4m),
            ("p3", 3, 6m),
            ("p4", 4, 8m),
            ("p5", 5, 10m));
        result.Should().OnlyContain(r => r.SchemeType == "Linear");
    }

    [Fact]
    public void Fibonacci_scheme_charges_fibonacci_percent_of_profit()
    {
        var result = CommissionCalculator.Calculate(Chain, 200m, new FibonacciScheme());

        result.Select(r => r.Amount).Should().Equal(2m, 2m, 4m, 6m, 10m);
        result.Should().OnlyContain(r => r.SchemeType == "Fibonacci");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    public void Non_positive_profit_produces_no_commissions(decimal profit) =>
        CommissionCalculator.Calculate(Chain, profit, new LinearScheme()).Should().BeEmpty();

    [Fact]
    public void Root_user_without_partners_produces_no_commissions() =>
        CommissionCalculator.Calculate([], 100m, new LinearScheme()).Should().BeEmpty();

    [Fact]
    public void Chain_order_does_not_matter()
    {
        var shuffled = Chain.Reverse().ToArray();

        CommissionCalculator.Calculate(shuffled, 100m, new LinearScheme()).Select(r => r.Level)
            .Should().Equal(1, 2, 3, 4, 5);
    }

    [Fact]
    public void Chain_with_gaps_is_rejected() =>
        FluentActions.Invoking(() => CommissionCalculator.Calculate([new("p1", 1), new("p3", 3)], 100m, new LinearScheme()))
            .Should().Throw<ArgumentException>();

    [Theory]
    [InlineData(1, 12.345, 0.1235)]
    [InlineData(1, 0.00005, 0.0000)]
    [InlineData(3, 0.00005, 0.0000)]
    [InlineData(1, 0.0049, 0.0000)]
    [InlineData(1, 0.005, 0.0001)]
    [InlineData(7, 1.3333, 0.0933)]
    public void Amount_is_rounded_to_four_digits_away_from_zero(int coefficient, decimal profit, decimal expected) =>
        CommissionCalculator.Amount(profit, coefficient).Should().Be(expected);

    [Fact]
    public void Amounts_that_round_to_zero_are_skipped_without_affecting_other_levels()
    {
        var result = CommissionCalculator.Calculate(Chain, 0.005m, new LinearScheme());

        result.Select(r => (r.Level, r.Amount)).Should().Equal((1, 0.0001m), (2, 0.0001m), (3, 0.0002m), (4, 0.0002m), (5, 0.0003m));

        CommissionCalculator.Calculate([new("p1", 1)], 0.0001m, new LinearScheme()).Should().BeEmpty();
    }

    [Fact]
    public void Deep_chain_uses_level_based_percent()
    {
        var deep = Enumerable.Range(1, 30).Select(l => new ChainMember($"p{l}", l)).ToArray();

        var result = CommissionCalculator.Calculate(deep, 10m, new FibonacciScheme());

        result.Should().HaveCount(30);
        result[^1].Amount.Should().Be(832040m * 10m / 100m);
    }

    [Fact]
    public void Overflow_is_reported_as_calculation_error()
    {
        var huge = Enumerable.Range(1, 200).Select(l => new ChainMember($"p{l}", l)).ToArray();

        FluentActions.Invoking(() => CommissionCalculator.Calculate(huge, 1_000_000m, new FibonacciScheme()))
            .Should().Throw<CommissionOverflowException>();
        FluentActions.Invoking(() => CommissionCalculator.Amount(99_999_999_999_999m, 200m))
            .Should().Throw<CommissionOverflowException>();
    }
}
