using System.Globalization;
using Acs.Protos.Commission;
using Commission.Api.Application;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;

namespace Commission.Api.Api;

internal sealed class CommissionGrpcService(EventCommissions commissions) : Acs.Protos.Commission.Commission.CommissionBase
{
    public override async Task<GetCommissionsForEventResponse> GetCommissionsForEvent(
        GetCommissionsForEventRequest request, ServerCallContext context)
    {
        var items = await commissions.GetAsync(request.EventExternalId, context.CancellationToken);
        var response = new GetCommissionsForEventResponse();
        response.Commissions.AddRange(items.Select(c =>
        {
            var item = new CommissionItem
            {
                CommissionId = c.Id.ToString(),
                BeneficiaryExternalId = c.BeneficiaryExternalId,
                Level = c.Level,
                Amount = c.Amount.ToString(CultureInfo.InvariantCulture),
                SchemeType = c.SchemeType,
                CalculatedAt = Timestamp.FromDateTimeOffset(c.CalculatedAt),
                IsPaid = c.IsPaid,
            };
            if (c.PaidAt is { } paidAt)
            {
                item.PaidAt = Timestamp.FromDateTimeOffset(paidAt);
            }

            return item;
        }));
        return response;
    }
}
