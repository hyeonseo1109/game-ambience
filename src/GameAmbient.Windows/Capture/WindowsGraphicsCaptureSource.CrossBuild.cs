using GameAmbient.Core.Domain;

namespace GameAmbient.Windows.Capture;

// Used only when cross-compiling the Windows UI on a non-Windows build host.
// Windows builds replace this with the Windows Graphics Capture implementation.
public sealed class WindowsGraphicsCaptureSource : IDisposable
{
    public bool IsRunning => false;
    public event EventHandler<PixelFrame>? RoiFrameArrived;
    public event EventHandler? TargetClosed;
    public event EventHandler? FrameSkipped;

    public Task<bool> PickAndStartAsync(IntPtr ownerWindow, NormalizedRect roi, int maximumHz = 8)
    {
        GC.KeepAlive(RoiFrameArrived);
        GC.KeepAlive(TargetClosed);
        GC.KeepAlive(FrameSkipped);
        throw new PlatformNotSupportedException("Live capture must run on Windows 10 version 2004 or later.");
    }

    public void Stop() { }
    public void Dispose() { }
}
