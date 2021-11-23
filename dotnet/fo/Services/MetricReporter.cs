using System.Diagnostics.Metrics;

public class MetricReporter : IMetricReporter
{
    private static readonly Meter Meter = new("Metrics", "1.0.0");
    private Counter<int> _counter;

    public MetricReporter() {
        System.Console.WriteLine("Instanciating MetricReporter()...");
        _counter = Meter.CreateCounter<int>("counter", "things", "I count things");
    }

    public Counter<int> GetCounter() {
        return _counter;
    }
}