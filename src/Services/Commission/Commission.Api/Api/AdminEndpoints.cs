using Commission.Api.Application;

namespace Commission.Api.Api;

public sealed record SwitchSchemeRequest(string? SchemeType);

internal static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapCommissionEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/admin").WithTags("Admin");

        admin.MapGet("/scheme", async (CommissionAdmin service, CancellationToken ct) =>
            Results.Ok(await service.GetActiveAsync(ct)));

        admin.MapPost("/scheme", async (SwitchSchemeRequest request, CommissionAdmin service, CancellationToken ct) =>
            Results.Ok(await service.SwitchAsync(request.SchemeType, ct)));

        return app;
    }
}
