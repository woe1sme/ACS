using Activity.Api.Application;
using Activity.Api.Infrastructure;
using BuildingBlocks.ServiceDefaults.Configuration;
using BuildingBlocks.ServiceDefaults.Messaging;
using BuildingBlocks.ServiceDefaults.Persistence;
using BuildingBlocks.ServiceDefaults.Resilience;
using Microsoft.Extensions.Options;

namespace Activity.Api;

internal static class DependencyInjection
{
    public static WebApplicationBuilder AddActivity(this WebApplicationBuilder builder)
    {
        builder.AddServiceDbContext<ActivityDbContext>("Activity");

        // Accepting events does not depend on the broker (outbox), so the broker is not part of readiness.
        builder.AddServiceMessaging<ActivityDbContext>(brokerAffectsReadiness: false);

        builder.Services.AddValidatedOptions<CommissionClientOptions>(builder.Configuration, "Commission");
        builder.Services
            .AddGrpcClient<Acs.Protos.Commission.Commission.CommissionClient>((sp, o) =>
                o.Address = new Uri(sp.GetRequiredService<IOptions<CommissionClientOptions>>().Value.GrpcAddress))
            .AddConfiguredResilienceHandler(builder.Configuration.GetSection("Commission:Resilience"));
        builder.Services.AddScoped<ICommissionsReader, GrpcCommissionsReader>();

        builder.Services.AddScoped<EventsService>();
        return builder;
    }
}
