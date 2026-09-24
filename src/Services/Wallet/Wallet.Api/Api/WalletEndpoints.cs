using BuildingBlocks.ServiceDefaults.Paging;
using Wallet.Api.Application;

namespace Wallet.Api.Api;

internal static class WalletEndpoints
{
    public static IEndpointRouteBuilder MapWalletEndpoints(this IEndpointRouteBuilder app)
    {
        var users = app.MapGroup("/users").WithTags("Wallet");

        users.MapGet("/{externalId}/balance", async (string externalId, WalletQueries queries, CancellationToken ct) =>
            Results.Ok(await queries.GetBalanceAsync(externalId, ct)));

        users.MapGet("/{externalId}/payouts", async (string externalId, int? page, int? pageSize, WalletQueries queries, CancellationToken ct) =>
            Results.Ok(await queries.GetPayoutsAsync(externalId, PageRequest.From(page, pageSize), ct)));

        return app;
    }
}
