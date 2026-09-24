using BuildingBlocks.ServiceDefaults.Configuration;
using BuildingBlocks.ServiceDefaults.Messaging;
using BuildingBlocks.ServiceDefaults.Persistence;
using Wallet.Api.Api;
using Wallet.Api.Application;
using Wallet.Api.Infrastructure;

namespace Wallet.Api;

internal static class DependencyInjection
{
    public static WebApplicationBuilder AddWallet(this WebApplicationBuilder builder)
    {
        builder.AddServiceDbContext<WalletDbContext>("Wallet");
        builder.AddServiceMessaging<WalletDbContext>(x => x.AddConsumer<CommissionAccruedConsumer>());

        builder.Services.AddValidatedOptions<PayoutOptions>(builder.Configuration, "Payout")
            .Validate(o => o.Interval > TimeSpan.Zero, "Payout:Interval must be positive.");
        builder.Services.AddSingleton<PayoutMetrics>();
        builder.Services.AddScoped<AccrueToWallet>();
        builder.Services.AddScoped<RunPayoutBatch>();
        builder.Services.AddScoped<WalletQueries>();
        builder.Services.AddHostedService<PayoutWorker>();
        return builder;
    }
}
