using GameAmbient.Core.Detection;
using GameAmbient.Core.Domain;
using GameAmbient.Core.Stabilization;

namespace GameAmbient.Core.Tests;

public sealed class SegmentedHeartDetectorTests
{
    [Theory]
    [InlineData(10, 0, 1.00)]
    [InlineData(5, 0, .50)]
    [InlineData(3, 1, .35)]
    [InlineData(1, 0, .10)]
    [InlineData(0, 0, .00)]
    public void Calculates_health_from_independent_slots(int full, int half, double expected)
    {
        var states = Enumerable.Repeat(HeartState.Full, full)
            .Concat(Enumerable.Repeat(HeartState.Half, half))
            .Concat(Enumerable.Repeat(HeartState.Empty, 10 - full - half)).ToArray();
        var frame = SyntheticHearts.Create(states);
        var detector = CreateDetector(frame, 10);
        var result = detector.DetectDetailed(frame, DateTimeOffset.UtcNow);
        Assert.NotNull(result.NormalizedValue);
        Assert.InRange(result.NormalizedValue!.Value, expected - .001, expected + .001);
        Assert.Equal(full, result.FullHearts);
        Assert.Equal(half, result.HalfHearts);
    }

    [Fact]
    public void Supports_nonstandard_slot_count_and_scaled_roi()
    {
        var states = new[] { HeartState.Full, HeartState.Full, HeartState.Half, HeartState.Empty, HeartState.Empty, HeartState.Empty };
        var frame = SyntheticHearts.Create(states, slotWidth: 24, height: 21);
        var result = CreateDetector(frame, states.Length).DetectDetailed(frame, DateTimeOffset.UtcNow);
        Assert.Equal(5, result.CurrentHealth);
        Assert.Equal(12, result.MaximumHealth);
        Assert.InRange(result.NormalizedValue!.Value, .416, .417);
    }

    [Fact]
    public void Brightness_variation_remains_detectable()
    {
        var states = Enumerable.Repeat(HeartState.Full, 4).Concat(Enumerable.Repeat(HeartState.Empty, 6)).ToArray();
        var frame = SyntheticHearts.Create(states, red: 150);
        var result = CreateDetector(frame, 10).DetectDetailed(frame, DateTimeOffset.UtcNow);
        Assert.InRange(result.NormalizedValue!.Value, .399, .401);
    }

    [Fact]
    public void Unknown_slot_invalidates_result_instead_of_becoming_empty()
    {
        var states = Enumerable.Repeat(HeartState.Full, 4).Append(HeartState.Unknown).Concat(Enumerable.Repeat(HeartState.Empty, 5)).ToArray();
        var frame = SyntheticHearts.Create(states);
        var result = CreateDetector(frame, 10).DetectDetailed(frame, DateTimeOffset.UtcNow);
        Assert.Null(result.NormalizedValue);
        Assert.Equal(1, result.UnknownHearts);
    }

    [Fact]
    public void Invalid_roi_returns_unknown()
    {
        var detector = new SegmentedHeartDetector(new SegmentedHeartDetectorOptions(
            [new HeartSlot(0, new NormalizedRect(0, 0, 1, 1))], []));
        var result = detector.Detect(new PixelFrame(0, 0, ReadOnlyMemory<RgbColor>.Empty), DateTimeOffset.UtcNow);
        Assert.Null(result.Value);
        Assert.Equal(0, result.Confidence);
    }

    [Fact]
    public void User_templates_survive_small_scale_and_color_changes()
    {
        var templateFrame = SyntheticHearts.Create([HeartState.Full, HeartState.Half, HeartState.Empty]);
        var slots = HeartSlotEstimator.Estimate(templateFrame, 3);
        var templates = statesToTemplates(templateFrame, slots);
        var detector = new SegmentedHeartDetector(new SegmentedHeartDetectorOptions(slots, templates, MinimumSlotConfidence: .45));
        var changed = SyntheticHearts.Create([HeartState.Full, HeartState.Half, HeartState.Empty], slotWidth: 16, height: 14, red: 185);
        var result = detector.DetectDetailed(changed, DateTimeOffset.UtcNow);
        Assert.Equal(new[] { HeartState.Full, HeartState.Half, HeartState.Empty }, result.Slots.Select(slot => slot.State));

        static IReadOnlyList<HeartTemplate> statesToTemplates(PixelFrame frame, IReadOnlyList<HeartSlot> slots)
        {
            var states = new[] { HeartState.Full, HeartState.Half, HeartState.Empty };
            return slots.Select((slot, index) => SegmentedHeartDetector.CreateTemplate(Crop(frame, slot.Bounds), states[index])).ToArray();
        }
    }

    [Fact]
    public void Detection_flows_into_warning_and_critical_rules()
    {
        var stabilizer = new MedianHysteresisStabilizer(new StabilizerOptions(MedianWindow: 1));
        var warningFrame = SyntheticHearts.Create(Enumerable.Repeat(HeartState.Full, 2).Concat(Enumerable.Repeat(HeartState.Empty, 8)).ToArray());
        var detector = CreateDetector(warningFrame, 10);
        Assert.Equal(HudState.Warning, stabilizer.Push(detector.Detect(warningFrame, DateTimeOffset.UtcNow)).State);
        var criticalFrame = SyntheticHearts.Create(Enumerable.Repeat(HeartState.Full, 1).Concat(Enumerable.Repeat(HeartState.Empty, 9)).ToArray());
        Assert.Equal(HudState.Critical, stabilizer.Push(detector.Detect(criticalFrame, DateTimeOffset.UtcNow)).State);
    }

    private static SegmentedHeartDetector CreateDetector(PixelFrame frame, int count) =>
        new(new SegmentedHeartDetectorOptions(HeartSlotEstimator.Estimate(frame, count), []));

    private static PixelFrame Crop(PixelFrame frame, NormalizedRect roi)
    {
        var rect = roi.ToPixels(frame.Width, frame.Height);
        var pixels = new RgbColor[rect.Width * rect.Height];
        for (var y = 0; y < rect.Height; y++)
        for (var x = 0; x < rect.Width; x++) pixels[(y * rect.Width) + x] = frame[rect.X + x, rect.Y + y];
        return new PixelFrame(rect.Width, rect.Height, pixels);
    }
}
