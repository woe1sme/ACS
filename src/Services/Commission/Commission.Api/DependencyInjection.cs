using BuildingBlocks.ServiceDefaults.Configuration;
using BuildingBlocks.ServiceDefaults.Grpc;
using BuildingBlocks.ServiceDefaults.Messaging;
using BuildingBlocks.ServiceDefaults.Persistence;
using BuildingBlocks.ServiceDefaults.Resilience;
using Commission.Api.Api;
using Commission.Api.Application;
using Commission.Api.Domain;
using Commission.Api.Infrastructure;
using Microsoft.Extensions.Options;

namespace Commission.Api;

internal static class DependencyInjection
{
    public static WebApplicationBuilder AddCommission(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<ICommissionScheme, LinearScheme>();
        builder.Services.AddSingleton<ICommissionScheme, FibonacciScheme>();
        builder.Services.AddSingleton<SchemeRegistry>();

        builder.AddServiceDbContext<CommissionDbContext>("Commission");
        builder.AddServiceMessaging<CommissionDbContext>(x =>
        {
            x.AddConsumer<ProfitPostedConsumer>();
            x.AddConsumer<CommissionPaidConsumer>();
        });

        builder.Services.AddValidatedOptions<PartnersClientOptions>(builder.Configuration, "Partners");
        builder.Services
            .AddGrpcClient<Acs.Protos.Partners.Partners.PartnersClient>((sp, o) =>
                o.Address = new Uri(sp.GetRequiredService<IOptions<PartnersClientOptions>>().Value.GrpcAddress))
            .AddConfiguredResilienceHandler(builder.Configuration.GetSection("Partners:Resilience"));
        builder.Services.AddScoped<IAncestorsProvider, PartnersAncestorsProvider>();

        builder.Services.AddScoped<AccrueCommissions>();
        builder.Services.AddScoped<ApplyPayment>();
        builder.Services.AddScoped<EventCommissions>();
        builder.Services.AddScoped<CommissionAdmin>();
        builder.Services.AddGrpcServerDefaults();
        return builder;
    }
}
