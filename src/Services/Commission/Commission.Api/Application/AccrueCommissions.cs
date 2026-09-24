using BuildingBlocks.Contracts;
using BuildingBlocks.ServiceDefaults.Errors;
using Commission.Api.Domain;
using Commission.Api.Infrastructure;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Commission.Api.Application;

public sealed class AccrueCommissions(
    CommissionDbContext db,
    IAncestorsProvider ancestors,
    SchemeRegistry schemes,
    TimeProvider clock,
    ILogger<AccrueCommissions> logger)
{
    /// <summary>Calculates and stores commissions for a profit event; all of them or none. Safe to repeat.</summary>
    public async Task<int> HandleAsync(ProfitPosted message, IPublishEndpoint publisher, CancellationToken ct)
    {
        if (message.Profit <= 0)
        {
            logger.LogWarning("Ignoring ProfitPosted {EventExternalId} with non-positive profit", message.EventExternalId);
            return 0;
        }

        if (await db.Commissions.AnyAsync(c => c.EventExternalId == message.EventExternalId, ct))
        {
            logger.LogInformation("Commissions for event {EventExternalId} already exist; skipping", message.EventExternalId);
            return 0;
        }

        var chain = await ancestors.GetAncestorsAsync(message.UserExternalId, ct);
        var settings = await db.SchemeSettings.SingleAsync(ct);
        var scheme = schemes.Get(settings.ActiveSchemeType);

        IReadOnlyList<CalculatedCommission> calculated;
        try
        {
            calculated = CommissionCalculator.Calculate(chain, message.Profit, scheme);
        }
        catch (CommissionOverflowException ex)
        {
            throw new NonRetryableException($"Commission calculation overflowed for event {message.EventExternalId}.", ex);
        }

        var now = clock.GetUtcNow();
        foreach (var item in calculated)
        {
            var record = CommissionRecord.Create(message.EventExternalId, item, now);
            db.Commissions.Add(record);
            await publisher.Publish(
                new CommissionAccrued(record.Id, record.EventExternalId, record.BeneficiaryExternalId, record.Amount, now), ct);
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation(
            "Accrued {Count} commissions for event {EventExternalId} using {SchemeType}",
            calculated.Count, message.EventExternalId, scheme.Type);
        return calculated.Count;
    }
}
