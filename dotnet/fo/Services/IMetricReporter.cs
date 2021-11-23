using System.Diagnostics.Metrics;

public interface IMetricReporter
{
    public Counter<int> GetCounter();
}