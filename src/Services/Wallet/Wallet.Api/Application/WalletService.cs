using System.Diagnostics.Metrics;
using BuildingBlocks.Contracts;
using BuildingBlocks.ServiceDefaults.Errors;
using BuildingBlocks.ServiceDefaults.Paging;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Wallet.Api.Domain;
using Wallet.Api.Infrastructure;

namespace Wallet.Api.Application;

public sealed record BalanceDto(string UserExternalId, decimal Balance);

public sealed record PayoutDto(Guid CommissionId, string EventExternalId, decimal Amount, DateTimeOffset PaidAt);

public sealed record PaidRow(Guid CommissionId, DateTimeOffset PaidAt);

public sealed class AccrueToWallet(WalletDbContext db, TimeProvider clock, ILogger<AccrueToWallet> logger)
{
    /// <summary>Stores a Pending wallet entry for the commission; repeats are ignored.</summary>
    public async Task HandleAsync(CommissionAccrued message, CancellationToken ct)
    {
        WalletEntry entry;
        try
        {
            entry = WalletEntry.Accrue(message.CommissionId, message.EventExternalId, message.BeneficiaryExternalId, message.Amount, clock.GetUtcNow());
        }
        catch (ValidationException ex)
        {
            throw new NonRetryableException($"Invalid CommissionAccrued {message.CommissionId}: {ex.Message}", ex);
        }

        var existing = await db.Entries.AsNoTracking().FirstOrDefaultAsync(e => e.CommissionId == message.CommissionId, ct);
        if (existing is not null)
        {
            if (existing.Amount != message.Amount)
            {
                logger.LogWarning(
                    "Duplicate CommissionAccrued {CommissionId} has amount {Amount} different from stored {Stored}",
                    message.CommissionId, message.Amount, existing.Amount);
            }

            return;
        }

        db.Entries.Add(entry);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Commission {CommissionId} accrued to wallet of {UserExternalId}", entry.CommissionId, entry.UserExternalId);
    }
}

public sealed class RunPayoutBatch(WalletDbContext db, IPublishEndpoint publisher, TimeProvider clock, PayoutMetrics metrics)
{
    /// <summary>
    /// Pays out up to <paramref name="batchSize"/> pending entries and enqueues CommissionPaid for each, in one transaction.
    /// FOR UPDATE SKIP LOCKED keeps concurrent replicas on disjoint batches. Returns the number of paid entries.
    /// </summary>
    public async Task<int> ExecuteAsync(int batchSize, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var paid = await db.Database.SqlQuery<PaidRow>(
            $"""
            UPDATE wallet_entries
            SET status = 'Paid', paid_at = {now}
            WHERE status = 'Pending' AND id IN (
                SELECT id FROM wallet_entries
                WHERE status = 'Pending'
                ORDER BY created_at, id
                LIMIT {batchSize}
                FOR UPDATE SKIP LOCKED)
            RETURNING commission_id, paid_at
            """).ToListAsync(ct);

        if (paid.Count == 0)
        {
            return 0;
        }

        foreach (var row in paid)
        {
            await publisher.Publish(new CommissionPaid(row.CommissionId, row.PaidAt), ct);
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        metrics.Paid(paid.Count);
        return paid.Count;
    }
}

public sealed class WalletQueries(WalletDbContext db)
{
    public async Task<BalanceDto> GetBalanceAsync(string userExternalId, CancellationToken ct)
    {
        Validate(userExternalId);
        var balance = await db.Entries
            .Where(e => e.UserExternalId == userExternalId && e.Status == WalletEntryStatus.Paid)
            .SumAsync(e => e.Amount, ct);
        return new BalanceDto(userExternalId, balance);
    }

    public async Task<PagedResponse<PayoutDto>> GetPayoutsAsync(string userExternalId, PageRequest page, CancellationToken ct)
    {
        Validate(userExternalId);
        var query = db.Entries.AsNoTracking()
            .Where(e => e.UserExternalId == userExternalId && e.Status == WalletEntryStatus.Paid);
        var total = await query.LongCountAsync(ct);
        var items = await query
            .OrderByDescending(e => e.PaidAt).ThenBy(e => e.Id)
            .Skip(page.Skip).Take(page.PageSize)
            .Select(e => new PayoutDto(e.CommissionId, e.EventExternalId, e.Amount, e.PaidAt!.Value))
            .ToListAsync(ct);
        return new PagedResponse<PayoutDto>(items, page.Page, page.PageSize, total);
    }

    private static void Validate(string userExternalId)
    {
        if (string.IsNullOrWhiteSpace(userExternalId) || userExternalId.Length > WalletEntry.UserIdMaxLength)
        {
            throw new ValidationException($"externalId must be 1-{WalletEntry.UserIdMaxLength} characters.");
        }
    }
}

public sealed class PayoutMetrics
{
    private readonly Counter<long> _paid;
    private readonly Counter<long> _failedTicks;

    public PayoutMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("Wallet.Api");
        _paid = meter.CreateCounter<long>("wallet_payout_entries_paid", description: "Wallet entries paid out");
        _failedTicks = meter.CreateCounter<long>("wallet_payout_ticks_failed", description: "Payout ticks that failed");
    }

    public void Paid(int count) => _paid.Add(count);

    public void TickFailed() => _failedTicks.Add(1);
}
