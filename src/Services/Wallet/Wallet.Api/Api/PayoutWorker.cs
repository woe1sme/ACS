using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;
using Wallet.Api.Application;

namespace Wallet.Api.Api;

public sealed class PayoutOptions
{
    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(1);

    [Range(1, 10_000)]
    public int BatchSize { get; set; } = 500;
}

/// <summary>Periodically pays out accrued commissions. Holds no logic beyond scheduling.</summary>
public sealed class PayoutWorker(
    IServiceScopeFactory scopes,
    IOptions<PayoutOptions> options,
    TimeProvider clock,
    PayoutMetrics metrics,
    ILogger<PayoutWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = options.Value.Interval > TimeSpan.Zero ? options.Value.Interval : TimeSpan.FromMinutes(1);
        using var timer = new PeriodicTimer(interval, clock);
        try
        {
            do
            {
                await RunTickAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Payout worker stopped");
        }
    }

    internal async Task RunTickAsync(CancellationToken stoppingToken)
    {
        var total = 0;
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await using var scope = scopes.CreateAsyncScope();
                var batch = scope.ServiceProvider.GetRequiredService<RunPayoutBatch>();
                var paid = await batch.ExecuteAsync(options.Value.BatchSize, stoppingToken);
                total += paid;
                if (paid < options.Value.BatchSize)
                {
                    break;
                }
            }

            if (total > 0)
            {
                logger.LogInformation("Payout tick paid {Count} wallet entries", total);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            metrics.TickFailed();
            logger.LogError(ex, "Payout tick failed after paying {Count} entries; the next tick will retry", total);
        }
    }
}
