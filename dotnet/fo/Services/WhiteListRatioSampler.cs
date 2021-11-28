using System;
using System.Collections.Generic;
using OpenTelemetry.Trace;



public sealed class WhiteListRatioSampler : Sampler
{
    private readonly string serviceName;
    private readonly List<string> allowedServices;
    private readonly double probability;

    public WhiteListRatioSampler(string serviceName, List<string> allowedServices, double probability)
    {
        this.serviceName = serviceName;
        this.allowedServices = allowedServices;
        this.probability = probability;
    }

    public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
    {
        Random reng = new Random();

        if (!allowedServices.Contains(serviceName) || this.probability > reng.NextDouble()) {
            return new SamplingResult(SamplingDecision.Drop);
        }

        return new SamplingResult(SamplingDecision.RecordAndSample);
    }
}
