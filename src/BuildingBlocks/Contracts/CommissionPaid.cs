namespace BuildingBlocks.Contracts;

/// <summary>A commission was paid out to the beneficiary's wallet by Wallet.</summary>
public sealed record CommissionPaid(
    Guid CommissionId,
    DateTimeOffset PaidAt);
