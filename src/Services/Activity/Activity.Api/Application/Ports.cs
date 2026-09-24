namespace Activity.Api.Application;

public sealed record EventCommissionDto(
    Guid CommissionId,
    string BeneficiaryExternalId,
    int Level,
    decimal Amount,
    string SchemeType,
    DateTimeOffset CalculatedAt,
    bool IsPaid,
    DateTimeOffset? PaidAt);

/// <summary>Reads commissions of an event from the Commission service.</summary>
public interface ICommissionsReader
{
    Task<IReadOnlyList<EventCommissionDto>> GetForEventAsync(string externalEventId, CancellationToken ct);
}
