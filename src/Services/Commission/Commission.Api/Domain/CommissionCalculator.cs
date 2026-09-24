namespace Commission.Api.Domain;

public sealed record ChainMember(string BeneficiaryExternalId, int Level);

public sealed record CalculatedCommission(string BeneficiaryExternalId, int Level, decimal Amount, string SchemeType);

/// <summary>The amount does not fit the storage precision; retrying cannot help.</summary>
public sealed class CommissionOverflowException(string message, Exception? inner = null) : Exception(message, inner);

public static class CommissionCalculator
{
    public const int Scale = 4;

    /// <summary>Largest value that fits numeric(18,4).</summary>
    public const decimal MaxAmount = 99_999_999_999_999.9999m;

    /// <summary>
    /// Calculates a commission for every partner above the event owner.
    /// Only positive profit produces commissions; amounts that round to zero are skipped.
    /// </summary>
    public static IReadOnlyList<CalculatedCommission> Calculate(IReadOnlyList<ChainMember> chain, decimal profit, ICommissionScheme scheme)
    {
        ArgumentNullException.ThrowIfNull(chain);
        ArgumentNullException.ThrowIfNull(scheme);
        if (profit <= 0)
        {
            return [];
        }

        var ordered = chain.OrderBy(m => m.Level).ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            if (ordered[i].Level != i + 1)
            {
                throw new ArgumentException("Partner chain levels must start at 1 and have no gaps.", nameof(chain));
            }
        }

        var result = new List<CalculatedCommission>(ordered.Count);
        foreach (var member in ordered)
        {
            decimal coefficient;
            try
            {
                coefficient = scheme.Coefficient(member.Level);
            }
            catch (OverflowException ex)
            {
                throw new CommissionOverflowException($"Scheme coefficient overflowed at level {member.Level}.", ex);
            }

            var amount = Amount(profit, coefficient);
            if (amount > 0)
            {
                result.Add(new CalculatedCommission(member.BeneficiaryExternalId, member.Level, amount, scheme.Type));
            }
        }

        return result;
    }

    public static decimal Amount(decimal profit, decimal coefficient)
    {
        try
        {
            var amount = Math.Round(checked(coefficient * profit) / 100m, Scale, MidpointRounding.AwayFromZero);
            return amount <= MaxAmount
                ? amount
                : throw new CommissionOverflowException($"Commission amount {amount} exceeds the supported maximum.");
        }
        catch (OverflowException ex)
        {
            throw new CommissionOverflowException("Commission amount overflowed.", ex);
        }
    }
}
