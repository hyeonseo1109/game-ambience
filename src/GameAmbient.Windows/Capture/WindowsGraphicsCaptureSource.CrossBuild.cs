using GameAmbient.Core.Domain;

namespace GameAmbient.Windows.Capture;

// Used only when cross-compiling the Windows UI on a non-Windows build host.
// Windows builds replace this with the Windows Graphics Capture implementation.
public sealed class WindowsGraphicsCaptureSource : IDisposable
{
    public bool IsRunning => false;
    public WindowTargetInfo? Target { get; private set; }
    public event EventHandler<PixelFrame>? RoiFrameArrived;
    public event EventHandler? TargetClosed;
    public event EventHandler? FrameSkipped;

    public Task<WindowTargetInfo?> PickTargetAsync(IntPtr ownerWindow)
    {
        GC.KeepAlive(RoiFrameArrived);
        GC.KeepAlive(TargetClosed);
        GC.KeepAlive(FrameSkipped);
        throw new PlatformNotSupportedException("Live capture must run on Windows 10 version 2004 or later.");
    }

    public void Start(NormalizedRect roi, int maximumHz = 8) => throw new PlatformNotSupportedException("Live capture must run on Windows.");
    public void SetPaused(bool paused) { }
    public void Stop() { }
    public void ClearTarget() => Target = null;
    public void Dispose() { }
}
