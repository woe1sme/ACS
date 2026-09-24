namespace BuildingBlocks.Contracts;

/// <summary>A profit event with a positive profit was accepted by Activity.</summary>
public sealed record ProfitPosted(
    string EventExternalId,
    string UserExternalId,
    decimal Profit,
    DateTimeOffset OccurredAt);
