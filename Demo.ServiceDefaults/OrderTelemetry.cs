using System.Diagnostics.Metrics;

namespace Demo.ServiceDefaults;

public static class OrderTelemetry
{
    private static readonly Meter Meter = new("Demo.Order");
    private static readonly Histogram<double> Duration =
        Meter.CreateHistogram<double>(
            "demo.order.duration",
            "ms",
            advice: new InstrumentAdvice<double>
            {
                HistogramBucketBoundaries = [100, 250, 500, 1_000, 2_500, 5_000, 10_000, 30_000, 60_000, 120_000]
            });
    private static readonly Counter<long> Outcomes =
        Meter.CreateCounter<long>("demo.order.outcomes");

    public static void RecordCompletion(double elapsedMilliseconds, string outcome)
    {
        var tags = new KeyValuePair<string, object?>("outcome", outcome);
        Duration.Record(elapsedMilliseconds, tags);
        Outcomes.Add(1, tags);
    }
}