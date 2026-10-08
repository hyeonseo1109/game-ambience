using GameAmbient.Core.Domain;

namespace GameAmbient.Core.Capture;

public sealed record CaptureTarget(string Id, string DisplayName, CaptureTargetKind Kind, string? ExecutableName = null, string? WindowTitleHint = null);
public sealed record CapturedFrame(PixelFrame Frame, DateTimeOffset Timestamp);

public interface ICaptureSource : IAsyncDisposable
{
    bool IsRunning { get; }
    event EventHandler<CapturedFrame>? FrameArrived;
    event EventHandler<string>? TargetLost;
    Task StartAsync(CaptureTarget target, CancellationToken cancellationToken);
    Task StopAsync();
}
