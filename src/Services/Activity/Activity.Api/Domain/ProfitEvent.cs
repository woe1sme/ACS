using BuildingBlocks.ServiceDefaults.Errors;

namespace Activity.Api.Domain;

/// <summary>Immutable profit/loss fact for a user.</summary>
public sealed class ProfitEvent
{
    public const int EventIdMaxLength = 128;
    public const int UserIdMaxLength = 64;
    public const decimal MaxAbsProfit = 99_999_999_999_999.9999m;

    private ProfitEvent()
    {
    }

    public string ExternalEventId { get; private set; } = null!;

    public string UserExternalId { get; private set; } = null!;

    public decimal Profit { get; private set; }

    public DateTimeOffset ReceivedAt { get; private set; }

    public static ProfitEvent Create(string? externalEventId, string? userExternalId, decimal? profit, DateTimeOffset receivedAt)
    {
        if (string.IsNullOrWhiteSpace(externalEventId) || externalEventId.Length > EventIdMaxLength)
        {
            throw new ValidationException($"externalEventId is required and must be at most {EventIdMaxLength} characters.");
        }

        if (string.IsNullOrWhiteSpace(userExternalId) || userExternalId.Length > UserIdMaxLength)
        {
            throw new ValidationException($"userExternalId is required and must be at most {UserIdMaxLength} characters.");
        }

        if (profit is not { } value)
        {
            throw new ValidationException("profit is required.");
        }

        if (Math.Abs(value) > MaxAbsProfit)
        {
            throw new ValidationException($"profit must be within ±{MaxAbsProfit}.");
        }

        if (decimal.Round(value, 4) != value)
        {
            throw new ValidationException("profit must have at most 4 digits after the decimal point.");
        }

        return new ProfitEvent
        {
            ExternalEventId = externalEventId,
            UserExternalId = userExternalId,
            Profit = value,
            ReceivedAt = receivedAt,
        };
    }

    public bool IsPositive => Profit > 0;

    public bool HasSamePayload(ProfitEvent other) =>
        UserExternalId == other.UserExternalId && Profit == other.Profit;
}
