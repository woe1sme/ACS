namespace Commission.Api.Domain;

public sealed class CommissionRecord
{
    private CommissionRecord()
    {
    }

    public Guid Id { get; private set; }

    public string EventExternalId { get; private set; } = null!;

    public string BeneficiaryExternalId { get; private set; } = null!;

    public int Level { get; private set; }

    public decimal Amount { get; private set; }

    public string SchemeType { get; private set; } = null!;

    public DateTimeOffset CalculatedAt { get; private set; }

    public bool IsPaid { get; private set; }

    public DateTimeOffset? PaidAt { get; private set; }

    public static CommissionRecord Create(string eventExternalId, CalculatedCommission calculated, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        EventExternalId = eventExternalId,
        BeneficiaryExternalId = calculated.BeneficiaryExternalId,
        Level = calculated.Level,
        Amount = calculated.Amount,
        SchemeType = calculated.SchemeType,
        CalculatedAt = now,
    };
}

public sealed class SchemeSettings
{
    public const short SingletonId = 1;

    private SchemeSettings()
    {
    }

    public short Id { get; private set; } = SingletonId;

    public string ActiveSchemeType { get; private set; } = null!;

    public DateTimeOffset UpdatedAt { get; private set; }

    public static SchemeSettings Initial(string schemeType, DateTimeOffset at) =>
        new() { ActiveSchemeType = schemeType, UpdatedAt = at };

    public bool SwitchTo(string schemeType, DateTimeOffset at)
    {
        if (ActiveSchemeType == schemeType)
        {
            return false;
        }

        ActiveSchemeType = schemeType;
        UpdatedAt = at;
        return true;
    }
}
