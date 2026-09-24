using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Activity.Api.Application;
using BuildingBlocks.ServiceDefaults.Errors;
using Microsoft.Extensions.Options;
using CommissionClient = Acs.Protos.Commission.Commission.CommissionClient;

namespace Activity.Api.Infrastructure;

public sealed class CommissionClientOptions
{
    [Required]
    [Url]
    public string GrpcAddress { get; set; } = null!;

    [Range(1, 120)]
    public double DeadlineSeconds { get; set; } = 3;
}

internal sealed class GrpcCommissionsReader(
    CommissionClient client,
    IOptions<CommissionClientOptions> options,
    ILogger<GrpcCommissionsReader> logger) : ICommissionsReader
{
    public async Task<IReadOnlyList<EventCommissionDto>> GetForEventAsync(string externalEventId, CancellationToken ct)
    {
        try
        {
            var response = await client.GetCommissionsForEventAsync(
                new Acs.Protos.Commission.GetCommissionsForEventRequest { EventExternalId = externalEventId },
                deadline: DateTime.UtcNow.AddSeconds(options.Value.DeadlineSeconds),
                cancellationToken: ct);

            return response.Commissions.Select(c => new EventCommissionDto(
                Guid.Parse(c.CommissionId),
                c.BeneficiaryExternalId,
                c.Level,
                decimal.Parse(c.Amount, CultureInfo.InvariantCulture),
                c.SchemeType,
                c.CalculatedAt.ToDateTimeOffset(),
                c.IsPaid,
                c.PaidAt?.ToDateTimeOffset())).ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Commission service call failed for event {ExternalEventId}", externalEventId);
            throw new DependencyUnavailableException("Commission details are temporarily unavailable.", ex);
        }
    }
}
