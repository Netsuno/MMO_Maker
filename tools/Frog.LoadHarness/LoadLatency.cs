namespace Frog.LoadHarness;

internal sealed class LoadLatencyCollector
{
    private readonly object _gate = new();
    private readonly List<double> _samples = [];

    public void Record(double milliseconds)
    {
        lock (_gate)
        {
            _samples.Add(milliseconds);
        }
    }

    public LoadLatencyStats Snapshot()
    {
        lock (_gate)
        {
            if (_samples.Count == 0)
            {
                return new LoadLatencyStats();
            }

            var ordered = _samples.OrderBy(x => x).ToArray();
            return new LoadLatencyStats
            {
                Count = ordered.Length,
                MeanMs = Math.Round(ordered.Average(), 2),
                P50Ms = Percentile(ordered, 0.50),
                P95Ms = Percentile(ordered, 0.95),
                P99Ms = Percentile(ordered, 0.99),
                MaxMs = Math.Round(ordered[^1], 2),
            };
        }
    }

    private static double Percentile(IReadOnlyList<double> ordered, double p)
    {
        if (ordered.Count == 1)
        {
            return Math.Round(ordered[0], 2);
        }

        var idx = (int)Math.Ceiling(p * ordered.Count) - 1;
        idx = Math.Clamp(idx, 0, ordered.Count - 1);
        return Math.Round(ordered[idx], 2);
    }
}
