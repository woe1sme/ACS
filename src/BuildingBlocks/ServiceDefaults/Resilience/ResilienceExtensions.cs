using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace BuildingBlocks.ServiceDefaults.Resilience;

public sealed class ClientResilienceOptions
{
    [Range(0, 10)]
    public int RetryAttempts { get; set; } = 2;

    [Range(1, 300)]
    public double AttemptTimeoutSeconds { get; set; } = 3;

    [Range(1, 600)]
    public double TotalTimeoutSeconds { get; set; } = 10;

    [Range(0.01, 1.0)]
    public double BreakerFailureRatio { get; set; } = 0.5;

    [Range(2, 10_000)]
    public int BreakerMinimumThroughput { get; set; } = 5;

    [Range(1, 3600)]
    public double BreakerBreakSeconds { get; set; } = 15;
}

public static class ResilienceExtensions
{
    /// <summary>Timeout, optional retry with exponential backoff and jitter, and circuit breaker; parameters come from configuration.</summary>
    public static IHttpClientBuilder AddConfiguredResilienceHandler(this IHttpClientBuilder builder, IConfigurationSection section)
    {
        var options = section.Get<ClientResilienceOptions>() ?? new ClientResilienceOptions();
        Validator.ValidateObject(options, new ValidationContext(options), validateAllProperties: true);

        builder.AddResilienceHandler("client", pipeline =>
        {
            pipeline.AddTimeout(TimeSpan.FromSeconds(options.TotalTimeoutSeconds));
            if (options.RetryAttempts > 0)
            {
                pipeline.AddRetry(new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = options.RetryAttempts,
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    Delay = TimeSpan.FromMilliseconds(200),
                });
            }

            pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
            {
                FailureRatio = options.BreakerFailureRatio,
                MinimumThroughput = options.BreakerMinimumThroughput,
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = TimeSpan.FromSeconds(options.BreakerBreakSeconds),
            });
            pipeline.AddTimeout(TimeSpan.FromSeconds(options.AttemptTimeoutSeconds));
        });

        return builder;
    }
}
