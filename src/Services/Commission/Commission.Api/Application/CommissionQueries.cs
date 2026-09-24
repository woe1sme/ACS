using BuildingBlocks.ServiceDefaults.Errors;
using Commission.Api.Domain;
using Commission.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Commission.Api.Application;

public sealed record SchemeDto(string SchemeType);

public sealed class CommissionAdmin(CommissionDbContext db, SchemeRegistry schemes, TimeProvider clock)
{
    public async Task<SchemeDto> GetActiveAsync(CancellationToken ct) =>
        new((await db.SchemeSettings.AsNoTracking().SingleAsync(ct)).ActiveSchemeType);

    /// <summary>Switches the active scheme; affects only commissions calculated afterwards.</summary>
    public async Task<SchemeDto> SwitchAsync(string? schemeType, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(schemeType) || !schemes.TryGet(schemeType, out var scheme))
        {
            throw new ValidationException($"schemeType must be one of: {string.Join(", ", schemes.Types)}.");
        }

        var settings = await db.SchemeSettings.SingleAsync(ct);
        if (settings.SwitchTo(scheme.Type, clock.GetUtcNow()))
        {
            await db.SaveChangesAsync(ct);
        }

        return new SchemeDto(settings.ActiveSchemeType);
    }
}

public sealed class EventCommissions(CommissionDbContext db)
{
    public async Task<IReadOnlyList<CommissionRecord>> GetAsync(string? eventExternalId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(eventExternalId))
        {
            throw new ValidationException("event_external_id is required.");
        }

        return await db.Commissions.AsNoTracking()
            .Where(c => c.EventExternalId == eventExternalId)
            .OrderBy(c => c.Level)
            .ToListAsync(ct);
    }
}

public sealed class ApplyPayment(CommissionDbContext db, ILogger<ApplyPayment> logger)
{
    /// <summary>Marks a commission as paid; repeated messages are no-ops and keep the first paid_at.</summary>
    public async Task HandleAsync(Guid commissionId, DateTimeOffset paidAt, CancellationToken ct)
    {
        var updated = await db.Commissions
            .Where(c => c.Id == commissionId && !c.IsPaid)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.IsPaid, true).SetProperty(c => c.PaidAt, paidAt), ct);

        if (updated == 1)
        {
            logger.LogInformation("Commission {CommissionId} marked as paid", commissionId);
            return;
        }

        if (!await db.Commissions.AnyAsync(c => c.Id == commissionId, ct))
        {
            throw new InvalidOperationException($"Commission {commissionId} does not exist.");
        }
    }
}
