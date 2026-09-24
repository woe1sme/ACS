using BuildingBlocks.ServiceDefaults.Paging;
using Partners.Api.Application;

namespace Partners.Api.Api;

public sealed record CreateUserRequest(string? ExternalId, string? ParentExternalId);

public sealed record ChangeParentRequest(string? ParentExternalId);

internal static class UserEndpoints
{
    public static IEndpointRouteBuilder MapPartnersEndpoints(this IEndpointRouteBuilder app)
    {
        var users = app.MapGroup("/users").WithTags("Users");

        users.MapPost("/", async (CreateUserRequest request, PartnersService service, CancellationToken ct) =>
        {
            var result = await service.CreateUserAsync(request.ExternalId, request.ParentExternalId, ct);
            return result.Created
                ? Results.Created($"/users/{result.User.ExternalId}", result.User)
                : Results.Ok(result.User);
        });

        users.MapPut("/{externalId}/parent", async (string externalId, ChangeParentRequest request, PartnersService service, CancellationToken ct) =>
            Results.Ok(await service.ChangeParentAsync(externalId, request.ParentExternalId, ct)));

        users.MapGet("/{externalId}/ancestors", async (string externalId, int? page, int? pageSize, PartnersService service, CancellationToken ct) =>
            Results.Ok(await service.GetAncestorsPageAsync(externalId, PageRequest.From(page, pageSize), ct)));

        users.MapGet("/{externalId}/descendants", async (string externalId, int? page, int? pageSize, PartnersService service, CancellationToken ct) =>
            Results.Ok(await service.GetDescendantsPageAsync(externalId, PageRequest.From(page, pageSize), ct)));

        return app;
    }
}
