using BuildingBlocks.Contracts;
using MassTransit;
using Wallet.Api.Application;

namespace Wallet.Api.Api;

public sealed class CommissionAccruedConsumer(AccrueToWallet accrue) : IConsumer<CommissionAccrued>
{
    public Task Consume(ConsumeContext<CommissionAccrued> context) => accrue.HandleAsync(context.Message, context.CancellationToken);
}
