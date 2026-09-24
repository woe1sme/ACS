using Acs.Protos.Partners;
using Grpc.Core;
using Partners.Api.Application;

namespace Partners.Api.Api;

internal sealed class PartnersGrpcService(PartnersService service) : Acs.Protos.Partners.Partners.PartnersBase
{
    public override async Task<GetAncestorsResponse> GetAncestors(GetAncestorsRequest request, ServerCallContext context)
    {
        var ancestors = await service.GetAncestorsAsync(request.UserExternalId, context.CancellationToken);
        var response = new GetAncestorsResponse();
        response.Ancestors.AddRange(ancestors.Select(a => new Ancestor { ExternalId = a.ExternalId, Level = a.Level }));
        return response;
    }
}
