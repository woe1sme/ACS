using BuildingBlocks.Contracts;
using Commission.Api.Application;
using MassTransit;

namespace Commission.Api.Api;

public sealed class ProfitPostedConsumer(AccrueCommissions accrue) : IConsumer<ProfitPosted>
{
    public Task Consume(ConsumeContext<ProfitPosted> context) =>
        accrue.HandleAsync(context.Message, context, context.CancellationToken);
}

public sealed class CommissionPaidConsumer(ApplyPayment apply) : IConsumer<CommissionPaid>
{
    public Task Consume(ConsumeContext<CommissionPaid> context) =>
        apply.HandleAsync(context.Message.CommissionId, context.Message.PaidAt, context.CancellationToken);
}
