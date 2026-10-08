using GameAmbient.Core.Detection;
using GameAmbient.Core.Domain;

namespace GameAmbient.Core.Tests;

public sealed class ColorBarDetectorTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private readonly ColorBarDetector _detector = new(new ColorBarDetectorOptions(new RgbColor(216, 52, 52)));

    [Theory]
    [InlineData(1.0)]
    [InlineData(0.5)]
    [InlineData(0.2)]
    public void Detects_contiguous_bar(double expected)
    {
        var result = _detector.Detect(SyntheticBar.Create(expected), Now);
        Assert.NotNull(result.Value);
        Assert.InRange(result.Value!.Value, expected - 0.02, expected + 0.02);
        Assert.True(result.Confidence >= 0.55);
    }

    [Fact]
    public void Accepts_small_hsv_variation()
    {
        var result = _detector.Detect(SyntheticBar.Create(0.63, color: new RgbColor(205, 60, 58)), Now);
        Assert.InRange(result.Value!.Value, 0.61, 0.65);
    }

    [Fact]
    public void Similar_object_after_empty_gap_is_not_counted_as_fill()
    {
        var frame = SyntheticBar.Create(0.3);
        var pixels = frame.Pixels.ToArray();
        for (var y = 0; y < frame.Height; y++)
        for (var x = 70; x < 80; x++) pixels[(y * frame.Width) + x] = new RgbColor(216, 52, 52);
        var result = _detector.Detect(frame with { Pixels = pixels }, Now);
        Assert.InRange(result.Value!.Value, 0.29, 0.31);
    }

    [Fact]
    public void Missing_target_is_unknown()
    {
        var result = _detector.Detect(SyntheticBar.Create(0), Now);
        Assert.Null(result.Value);
        Assert.Equal(0, result.Confidence);
    }
}
