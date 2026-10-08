using GameAmbient.Core.Domain;

namespace GameAmbient.Core.Detection;

public enum HeartState { Full, Half, Empty, Unknown }

public sealed record HeartSlot(int Index, NormalizedRect Bounds);

public sealed record HeartTemplate(HeartState State, int GridSize, IReadOnlyList<double> Features, string Source = "user");

public sealed record SegmentedHeartDetectorOptions(
    IReadOnlyList<HeartSlot> Slots,
    IReadOnlyList<HeartTemplate> Templates,
    double MinimumSlotConfidence = .55,
    double MaximumUnknownFraction = 0,
    int FeatureGridSize = 8);

public sealed record HeartSlotResult(
    int Index,
    HeartState State,
    double Confidence,
    double RedCoverage,
    HeartState? ClosestTemplate,
    double TemplateScore);

public sealed record HeartDetectionResult(
    IReadOnlyList<HeartSlotResult> Slots,
    int FullHearts,
    int HalfHearts,
    int EmptyHearts,
    int UnknownHearts,
    int CurrentHealth,
    int MaximumHealth,
    double? NormalizedValue,
    double Confidence,
    DateTimeOffset Timestamp)
{
    public DetectionResult ToDetectionResult() => NormalizedValue is null
        ? DetectionResult.Unknown(Timestamp)
        : new DetectionResult(NormalizedValue, Confidence, Timestamp);
}
