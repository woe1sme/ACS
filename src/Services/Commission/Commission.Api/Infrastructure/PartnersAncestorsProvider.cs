using System.ComponentModel.DataAnnotations;
using Acs.Protos.Partners;
using Commission.Api.Application;
using Commission.Api.Domain;
using Microsoft.Extensions.Options;

namespace Commission.Api.Infrastructure;

public sealed class PartnersClientOptions
{
    [Required]
    [Url]
    public string GrpcAddress { get; set; } = null!;

    [Range(1, 120)]
    public double DeadlineSeconds { get; set; } = 5;
}

internal sealed class PartnersAncestorsProvider(Partners.PartnersClient client, IOptions<PartnersClientOptions> options)
    : IAncestorsProvider
{
    public async Task<IReadOnlyList<ChainMember>> GetAncestorsAsync(string userExternalId, CancellationToken ct)
    {
        var response = await client.GetAncestorsAsync(
            new GetAncestorsRequest { UserExternalId = userExternalId },
            deadline: DateTime.UtcNow.AddSeconds(options.Value.DeadlineSeconds),
            cancellationToken: ct);

        return response.Ancestors.Select(a => new ChainMember(a.ExternalId, a.Level)).ToList();
    }
}
