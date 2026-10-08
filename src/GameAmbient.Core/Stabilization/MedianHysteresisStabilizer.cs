using GameAmbient.Core.Domain;

namespace GameAmbient.Core.Stabilization;

public sealed class MedianHysteresisStabilizer : ISignalStabilizer
{
    private readonly StabilizerOptions _options;
    private readonly Queue<double> _samples = new();
    private HudState _state = HudState.Unknown;

    public MedianHysteresisStabilizer(StabilizerOptions options)
    {
        if (options.MedianWindow < 1 || options.WarningEnter >= options.WarningExit || options.CriticalEnter >= options.CriticalExit || options.CriticalEnter >= options.WarningEnter)
            throw new ArgumentException("Invalid stabilization thresholds.", nameof(options));
        _options = options;
    }

    public StabilizedSignal Push(DetectionResult result)
    {
        if (result.Value is null || result.Confidence < _options.MinimumConfidence)
            return new StabilizedSignal(null, result.Confidence, HudState.Unknown, result.Timestamp);

        _samples.Enqueue(Math.Clamp(result.Value.Value, 0, 1));
        while (_samples.Count > _options.MedianWindow) _samples.Dequeue();

        var ordered = _samples.Order().ToArray();
        var middle = ordered.Length / 2;
        var median = ordered.Length % 2 == 1 ? ordered[middle] : (ordered[middle - 1] + ordered[middle]) / 2;
        _state = NextState(_state, median);
        return new StabilizedSignal(median, result.Confidence, _state, result.Timestamp);
    }

    public void Reset()
    {
        _samples.Clear();
        _state = HudState.Unknown;
    }

    private HudState NextState(HudState current, double value)
    {
        if (current == HudState.Critical && value < _options.CriticalExit) return HudState.Critical;
        if (value <= _options.CriticalEnter) return HudState.Critical;
        if (current is HudState.Warning or HudState.Critical && value < _options.WarningExit) return HudState.Warning;
        if (value <= _options.WarningEnter) return HudState.Warning;
        return HudState.Safe;
    }
}
