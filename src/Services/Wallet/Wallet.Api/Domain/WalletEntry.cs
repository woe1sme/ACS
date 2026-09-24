using BuildingBlocks.ServiceDefaults.Errors;

namespace Wallet.Api.Domain;

public static class WalletEntryStatus
{
    public const string Pending = "Pending";
    public const string Paid = "Paid";
}

/// <summary>A commission accrued to a user's wallet. Pending until the periodic payout moves it to Paid.</summary>
public sealed class WalletEntry
{
    public const int UserIdMaxLength = 64;
    public const int EventIdMaxLength = 128;
    public const decimal MaxAmount = 99_999_999_999_999.9999m;

    private WalletEntry()
    {
    }

    public Guid Id { get; private set; }

    public string UserExternalId { get; private set; } = null!;

    public Guid CommissionId { get; private set; }

    public string EventExternalId { get; private set; } = null!;

    public decimal Amount { get; private set; }

    public string Status { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? PaidAt { get; private set; }

    public static WalletEntry Accrue(Guid commissionId, string? eventExternalId, string? beneficiaryExternalId, decimal amount, DateTimeOffset now)
    {
        if (commissionId == Guid.Empty)
        {
            throw new ValidationException("CommissionId is required.");
        }

        if (string.IsNullOrWhiteSpace(beneficiaryExternalId) || beneficiaryExternalId.Length > UserIdMaxLength)
        {
            throw new ValidationException("BeneficiaryExternalId is invalid.");
        }

        if (string.IsNullOrWhiteSpace(eventExternalId) || eventExternalId.Length > EventIdMaxLength)
        {
            throw new ValidationException("EventExternalId is invalid.");
        }

        if (amount <= 0 || amount > MaxAmount || decimal.Round(amount, 4) != amount)
        {
            throw new ValidationException("Amount must be positive and fit numeric(18,4).");
        }

        return new WalletEntry
        {
            Id = Guid.NewGuid(),
            CommissionId = commissionId,
            EventExternalId = eventExternalId,
            UserExternalId = beneficiaryExternalId,
            Amount = amount,
            Status = WalletEntryStatus.Pending,
            CreatedAt = now,
        };
    }
}
