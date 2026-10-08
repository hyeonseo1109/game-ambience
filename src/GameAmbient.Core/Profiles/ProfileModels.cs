using GameAmbient.Core.Detection;
using GameAmbient.Core.Domain;
using GameAmbient.Core.Stabilization;

namespace GameAmbient.Core.Profiles;

public sealed record GameProfile(
    int SchemaVersion,
    Guid Id,
    string Name,
    CaptureTarget Target,
    NormalizedRect Roi,
    ColorBarDetectorOptions Detector,
    StabilizerOptions Stabilizer,
    AmbientEffectOptions Ambient,
    PerformancePreset Performance = PerformancePreset.Balanced,
    CalibrationMetadata? Calibration = null)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record CaptureTarget(CaptureTargetKind Kind, string? ExecutableName, string? WindowTitleHint, string? MonitorDeviceName);
public sealed record AmbientEffectOptions(RgbColor WarningColor, RgbColor CriticalColor, double MaximumOpacity = 0.42, double WarningCycleSeconds = 2, double CriticalCycleSeconds = 1, double GlowWidth = 0.16);
public sealed record CalibrationMetadata(int SourceWidth, int SourceHeight, DateTimeOffset CalibratedAt);

public sealed record ProfileValidationResult(bool IsValid, IReadOnlyList<string> Errors);
