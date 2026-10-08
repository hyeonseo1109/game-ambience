using GameAmbient.Core.Domain;

namespace GameAmbient.Core.Stabilization;

public interface ISignalStabilizer
{
    StabilizedSignal Push(DetectionResult result);
    void Reset();
}

public sealed record StabilizerOptions(
    int MedianWindow = 5,
    double MinimumConfidence = 0.55,
    double WarningEnter = 0.30,
    double WarningExit = 0.40,
    double CriticalEnter = 0.15,
    double CriticalExit = 0.20);
