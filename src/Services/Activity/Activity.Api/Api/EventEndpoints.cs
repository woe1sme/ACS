using Activity.Api.Application;
using BuildingBlocks.ServiceDefaults.Paging;

namespace Activity.Api.Api;

public sealed record AcceptEventRequest(string? ExternalEventId, string? UserExternalId, decimal? Profit);

internal static class EventEndpoints
{
    public static IEndpointRouteBuilder MapActivityEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/events", async (AcceptEventRequest request, EventsService service, CancellationToken ct) =>
        {
            var result = await service.AcceptAsync(request.ExternalEventId, request.UserExternalId, request.Profit, ct);
            return result.Created
                ? Results.Created($"/events/{Uri.EscapeDataString(result.Event.ExternalEventId)}", result.Event)
                : Results.Ok(result.Event);
        }).WithTags("Events");

        app.MapGet("/events/{externalEventId}", async (string externalEventId, EventsService service, CancellationToken ct) =>
            Results.Ok(await service.GetDetailAsync(externalEventId, ct))).WithTags("Events");

        app.MapGet("/users/{externalId}/events", async (string externalId, int? page, int? pageSize, EventsService service, CancellationToken ct) =>
            Results.Ok(await service.ListForUserAsync(externalId, PageRequest.From(page, pageSize), ct))).WithTags("Events");

        return app;
    }
}
