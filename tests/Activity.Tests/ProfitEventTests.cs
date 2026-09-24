using Activity.Api.Domain;
using BuildingBlocks.ServiceDefaults.Errors;

namespace Activity.Tests;

public class ProfitEventTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(100, true)]
    [InlineData(0.0001, true)]
    [InlineData(0, false)]
    [InlineData(-25.5, false)]
    public void Only_positive_profit_is_published(decimal profit, bool positive) =>
        ProfitEvent.Create("e1", "u1", profit, Now).IsPositive.Should().Be(positive);

    [Theory]
    [InlineData(null, "u1", 1)]
    [InlineData(" ", "u1", 1)]
    [InlineData("e1", null, 1)]
    [InlineData("e1", "", 1)]
    [InlineData("e1", "u1", null)]
    public void Rejects_missing_fields(string? eventId, string? userId, int? profit) =>
        FluentActions.Invoking(() => ProfitEvent.Create(eventId, userId, profit, Now)).Should().Throw<ValidationException>();

    [Fact]
    public void Rejects_more_than_four_decimal_places()
    {
        FluentActions.Invoking(() => ProfitEvent.Create("e1", "u1", 1.23456m, Now)).Should().Throw<ValidationException>();
        ProfitEvent.Create("e1", "u1", 1.23450m, Now).Profit.Should().Be(1.2345m);
    }

    [Fact]
    public void Rejects_too_long_identifiers()
    {
        FluentActions.Invoking(() => ProfitEvent.Create(new string('e', 129), "u1", 1, Now)).Should().Throw<ValidationException>();
        FluentActions.Invoking(() => ProfitEvent.Create("e1", new string('u', 65), 1, Now)).Should().Throw<ValidationException>();
    }

    [Fact]
    public void Same_payload_ignores_receive_time_and_scale()
    {
        var first = ProfitEvent.Create("e1", "u1", 10m, Now);

        first.HasSamePayload(ProfitEvent.Create("e1", "u1", 10.00m, Now.AddMinutes(5))).Should().BeTrue();
        first.HasSamePayload(ProfitEvent.Create("e1", "u1", 11m, Now)).Should().BeFalse();
        first.HasSamePayload(ProfitEvent.Create("e1", "u2", 10m, Now)).Should().BeFalse();
    }
}
