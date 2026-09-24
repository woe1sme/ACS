using System.ComponentModel.DataAnnotations;
using BuildingBlocks.ServiceDefaults.Configuration;
using BuildingBlocks.ServiceDefaults.Errors;
using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.ServiceDefaults.Messaging;

public sealed class RabbitMqOptions
{
    [Required]
    public string Host { get; set; } = "localhost";

    [Range(1, 65535)]
    public ushort Port { get; set; } = 5672;

    [Required]
    public string VirtualHost { get; set; } = "/";

    [Required]
    public string Username { get; set; } = "guest";

    [Required]
    public string Password { get; set; } = "guest";
}

public sealed class MessageRetryOptions
{
    [Range(0, 20)]
    public int RetryLimit { get; set; } = 5;

    public TimeSpan MinInterval { get; set; } = TimeSpan.FromSeconds(1);

    public TimeSpan MaxInterval { get; set; } = TimeSpan.FromSeconds(30);

    public TimeSpan IntervalDelta { get; set; } = TimeSpan.FromSeconds(2);
}

public static class MessagingExtensions
{
    /// <summary>
    /// Configures MassTransit on RabbitMQ with the EF Core transactional outbox (bus outbox + consumer inbox/outbox)
    /// and exponential message retry. Exhausted or non-retryable messages end up in the endpoint's _error queue.
    /// </summary>
    public static WebApplicationBuilder AddServiceMessaging<TContext>(
        this WebApplicationBuilder builder,
        Action<IBusRegistrationConfigurator>? configureConsumers = null,
        bool brokerAffectsReadiness = true)
        where TContext : DbContext
    {
        builder.Services.AddValidatedOptions<RabbitMqOptions>(builder.Configuration, "RabbitMq");
        builder.Services.AddValidatedOptions<MessageRetryOptions>(builder.Configuration, "MessageRetry");

        var rabbit = builder.Configuration.GetSection("RabbitMq").Get<RabbitMqOptions>() ?? new RabbitMqOptions();
        var retry = builder.Configuration.GetSection("MessageRetry").Get<MessageRetryOptions>() ?? new MessageRetryOptions();

        builder.Services.AddMassTransit(x =>
        {
            x.SetKebabCaseEndpointNameFormatter();

            x.AddEntityFrameworkOutbox<TContext>(o =>
            {
                o.UsePostgres();
                o.UseBusOutbox();
                o.QueryDelay = TimeSpan.FromSeconds(1);
                o.DuplicateDetectionWindow = TimeSpan.FromDays(1);
            });

            x.AddConfigureEndpointsCallback((context, _, endpoint) =>
            {
                endpoint.UseMessageRetry(r =>
                {
                    r.Exponential(retry.RetryLimit, retry.MinInterval, retry.MaxInterval, retry.IntervalDelta);
                    r.Ignore<NonRetryableException>();
                });
                endpoint.UseEntityFrameworkOutbox<TContext>(context);
            });

            configureConsumers?.Invoke(x);

            if (!brokerAffectsReadiness)
            {
                x.ConfigureHealthCheckOptions(o =>
                {
                    o.Tags.Clear();
                    o.Tags.Add("masstransit");
                });
            }

            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(rabbit.Host, rabbit.Port, rabbit.VirtualHost, h =>
                {
                    h.Username(rabbit.Username);
                    h.Password(rabbit.Password);
                });
                cfg.ConfigureEndpoints(context);
            });
        });

        builder.Services.Configure<MassTransitHostOptions>(o =>
        {
            o.WaitUntilStarted = false;
            o.StopTimeout = TimeSpan.FromSeconds(20);
        });

        return builder;
    }
}
