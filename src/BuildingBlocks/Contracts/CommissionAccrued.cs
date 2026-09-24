namespace BuildingBlocks.Contracts;

/// <summary>A commission was calculated and stored by Commission.</summary>
public sealed record CommissionAccrued(
    Guid CommissionId,
    string EventExternalId,
    string BeneficiaryExternalId,
    decimal Amount,
    DateTimeOffset AccruedAt);
