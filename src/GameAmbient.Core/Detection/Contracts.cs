using GameAmbient.Core.Domain;

namespace GameAmbient.Core.Detection;

public interface IDetector
{
    string Kind { get; }
    DetectionResult Detect(PixelFrame frame, DateTimeOffset timestamp);
}

public sealed record ColorBarDetectorOptions(
    RgbColor TargetColor,
    FillDirection FillDirection = FillDirection.LeftToRight,
    double HueTolerance = 18,
    double SaturationTolerance = 0.35,
    double ValueTolerance = 0.35,
    double LineMatchThreshold = 0.45,
    double MinimumConfidence = 0.55,
    int MaxGap = 2);
