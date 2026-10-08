using GameAmbient.Core.Domain;
using GameAmbient.Core.Stabilization;

namespace GameAmbient.Core.Tests;

public sealed class StabilizerTests
{
    [Fact]
    public void Median_rejects_outlier()
    {
        var stabilizer = new MedianHysteresisStabilizer(new StabilizerOptions());
        StabilizedSignal? result = null;
        foreach (var value in new[] { .30, .31, .80, .29, .30 }) result = stabilizer.Push(Sample(value));
        Assert.InRange(result!.Value!.Value, .299, .301);
    }

    [Fact]
    public void Hysteresis_keeps_warning_until_exit_threshold()
    {
        var stabilizer = new MedianHysteresisStabilizer(new StabilizerOptions(MedianWindow: 1));
        Assert.Equal(HudState.Warning, stabilizer.Push(Sample(.29)).State);
        Assert.Equal(HudState.Warning, stabilizer.Push(Sample(.32)).State);
        Assert.Equal(HudState.Warning, stabilizer.Push(Sample(.39)).State);
        Assert.Equal(HudState.Safe, stabilizer.Push(Sample(.41)).State);
    }

    [Fact]
    public void Low_confidence_is_unknown_and_does_not_replace_previous_state()
    {
        var stabilizer = new MedianHysteresisStabilizer(new StabilizerOptions(MedianWindow: 1));
        Assert.Equal(HudState.Warning, stabilizer.Push(Sample(.25)).State);
        Assert.Equal(HudState.Unknown, stabilizer.Push(new DetectionResult(0, .1, DateTimeOffset.UtcNow)).State);
        Assert.Equal(HudState.Warning, stabilizer.Push(Sample(.35)).State);
    }

    private static DetectionResult Sample(double value) => new(value, .95, DateTimeOffset.UtcNow);
}
