using Activity.Api.Domain;
using Activity.Api.Infrastructure;
using BuildingBlocks.Contracts;
using BuildingBlocks.ServiceDefaults.Errors;
using BuildingBlocks.ServiceDefaults.Paging;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Activity.Api.Application;

public sealed record EventDto(string ExternalEventId, string UserExternalId, decimal Profit, DateTimeOffset ReceivedAt);

public sealed record UserEventDto(string ExternalEventId, decimal Profit, DateTimeOffset ReceivedAt);

public sealed record EventDetailDto(
    string ExternalEventId,
    string UserExternalId,
    decimal Profit,
    DateTimeOffset ReceivedAt,
    IReadOnlyList<EventCommissionDto> Commissions);

public sealed record AcceptResult(EventDto Event, bool Created);

public sealed class EventsService(
    ActivityDbContext db,
    IPublishEndpoint publisher,
    ICommissionsReader commissions,
    TimeProvider clock,
    ILogger<EventsService> logger)
{
    /// <summary>Stores the event and, for positive profit, enqueues ProfitPosted in the same transaction. Idempotent by id.</summary>
    public async Task<AcceptResult> AcceptAsync(string? externalEventId, string? userExternalId, decimal? profit, CancellationToken ct)
    {
        var incoming = ProfitEvent.Create(externalEventId, userExternalId, profit, clock.GetUtcNow());

        var existing = await db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.ExternalEventId == incoming.ExternalEventId, ct);
        if (existing is not null)
        {
            return ResolveRepeat(existing, incoming);
        }

        db.Events.Add(incoming);
        if (incoming.IsPositive)
        {
            await publisher.Publish(
                new ProfitPosted(incoming.ExternalEventId, incoming.UserExternalId, incoming.Profit, incoming.ReceivedAt), ct);
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            db.ChangeTracker.Clear();
            var raced = await db.Events.AsNoTracking().SingleAsync(e => e.ExternalEventId == incoming.ExternalEventId, ct);
            return ResolveRepeat(raced, incoming);
        }

        logger.LogInformation("Accepted event {ExternalEventId} (published: {Published})", incoming.ExternalEventId, incoming.IsPositive);
        return new AcceptResult(ToDto(incoming), Created: true);
    }

    public async Task<PagedResponse<UserEventDto>> ListForUserAsync(string userExternalId, PageRequest page, CancellationToken ct)
    {
        var query = db.Events.AsNoTracking().Where(e => e.UserExternalId == userExternalId);
        var total = await query.LongCountAsync(ct);
        var items = await query
            .OrderByDescending(e => e.ReceivedAt).ThenBy(e => e.ExternalEventId)
            .Skip(page.Skip).Take(page.PageSize)
            .Select(e => new UserEventDto(e.ExternalEventId, e.Profit, e.ReceivedAt))
            .ToListAsync(ct);
        return new PagedResponse<UserEventDto>(items, page.Page, page.PageSize, total);
    }

    public async Task<EventDetailDto> GetDetailAsync(string externalEventId, CancellationToken ct)
    {
        var e = await db.Events.AsNoTracking().FirstOrDefaultAsync(x => x.ExternalEventId == externalEventId, ct)
            ?? throw new NotFoundException($"Event '{externalEventId}' was not found.");

        var items = await commissions.GetForEventAsync(e.ExternalEventId, ct);
        return new EventDetailDto(e.ExternalEventId, e.UserExternalId, e.Profit, e.ReceivedAt, items);
    }

    private AcceptResult ResolveRepeat(ProfitEvent stored, ProfitEvent incoming)
    {
        if (!stored.HasSamePayload(incoming))
        {
            throw new ConflictException($"Event '{incoming.ExternalEventId}' was already accepted with different data.");
        }

        logger.LogInformation("Event {ExternalEventId} is a repeat; nothing to do", incoming.ExternalEventId);
        return new AcceptResult(ToDto(stored), Created: false);
    }

    private static EventDto ToDto(ProfitEvent e) => new(e.ExternalEventId, e.UserExternalId, e.Profit, e.ReceivedAt);
}
