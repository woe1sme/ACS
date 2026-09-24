using System.Diagnostics;
using OpenTelemetry.Trace;

namespace BuildingBlocks.ServiceDefaults.Telemetry;

/// <summary>
/// Drops traces that start with a client span, e.g. database polling by the outbox delivery service,
/// so that traces show business flows (HTTP requests, gRPC calls, consumed messages) rather than background noise.
/// </summary>
internal sealed class SkipBackgroundClientSpansSampler : Sampler
{
    public override SamplingResult ShouldSample(in SamplingParameters samplingParameters) =>
        samplingParameters.Kind == ActivityKind.Client
            ? new SamplingResult(SamplingDecision.Drop)
            : new SamplingResult(SamplingDecision.RecordAndSample);
}
