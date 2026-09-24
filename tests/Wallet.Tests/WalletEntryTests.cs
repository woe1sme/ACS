using BuildingBlocks.ServiceDefaults.Errors;
using Wallet.Api.Domain;

namespace Wallet.Tests;

public class WalletEntryTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Accrued_entry_is_pending_and_not_paid()
    {
        var entry = WalletEntry.Accrue(Guid.NewGuid(), "e1", "u1", 1.5m, Now);

        entry.Status.Should().Be(WalletEntryStatus.Pending);
        entry.PaidAt.Should().BeNull();
        entry.CreatedAt.Should().Be(Now);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(0.00001)]
    public void Rejects_invalid_amount(decimal amount) =>
        FluentActions.Invoking(() => WalletEntry.Accrue(Guid.NewGuid(), "e1", "u1", amount, Now)).Should().Throw<ValidationException>();

    [Fact]
    public void Rejects_empty_identifiers()
    {
        FluentActions.Invoking(() => WalletEntry.Accrue(Guid.Empty, "e1", "u1", 1, Now)).Should().Throw<ValidationException>();
        FluentActions.Invoking(() => WalletEntry.Accrue(Guid.NewGuid(), "", "u1", 1, Now)).Should().Throw<ValidationException>();
        FluentActions.Invoking(() => WalletEntry.Accrue(Guid.NewGuid(), "e1", " ", 1, Now)).Should().Throw<ValidationException>();
    }
}
