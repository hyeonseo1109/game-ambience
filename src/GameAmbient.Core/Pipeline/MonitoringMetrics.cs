namespace GameAmbient.Core.Pipeline;

public sealed class MonitoringMetrics
{
    private long _processed;
    private long _skipped;
    private double _totalMilliseconds;
    private double _maxMilliseconds;
    private readonly object _gate = new();

    public void RecordProcessed(TimeSpan duration)
    {
        lock (_gate)
        {
            _processed++;
            _totalMilliseconds += duration.TotalMilliseconds;
            _maxMilliseconds = Math.Max(_maxMilliseconds, duration.TotalMilliseconds);
        }
    }

    public void RecordSkipped() => Interlocked.Increment(ref _skipped);

    public MetricsSnapshot Snapshot(double elapsedSeconds)
    {
        lock (_gate)
        {
            return new MetricsSnapshot(_processed, _skipped, elapsedSeconds <= 0 ? 0 : _processed / elapsedSeconds,
                _processed == 0 ? 0 : _totalMilliseconds / _processed, _maxMilliseconds);
        }
    }
}

public sealed record MetricsSnapshot(long ProcessedFrames, long SkippedFrames, double AnalysisHz, double AverageDetectorMilliseconds, double MaxDetectorMilliseconds);
