using System.Runtime.InteropServices;
using GameAmbient.Core.Domain;
using Microsoft.Graphics.Canvas;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;

namespace GameAmbient.Windows.Capture;

public sealed class WindowsGraphicsCaptureSource : IDisposable
{
    private readonly CanvasDevice _device = new();
    private Direct3D11CaptureFramePool? _framePool;
    private GraphicsCaptureSession? _session;
    private GraphicsCaptureItem? _item;
    private NormalizedRect _roi;
    private long _lastFrameTicks;
    private int _processing;

    public bool IsRunning => _session is not null;
    public event EventHandler<PixelFrame>? RoiFrameArrived;
    public event EventHandler? TargetClosed;
    public event EventHandler? FrameSkipped;

    public async Task<bool> PickAndStartAsync(IntPtr ownerWindow, NormalizedRect roi, int maximumHz = 8)
    {
        if (!GraphicsCaptureSession.IsSupported()) throw new NotSupportedException("Windows Graphics Capture is not supported on this system.");
        if (!roi.IsValid) throw new ArgumentException("A valid normalized ROI is required.", nameof(roi));
        Stop();

        var picker = new GraphicsCapturePicker();
        WinRT.Interop.InitializeWithWindow.Initialize(picker, ownerWindow);
        _item = await picker.PickSingleItemAsync();
        if (_item is null) return false;

        _roi = roi;
        MinimumFrameIntervalTicks = TimeSpan.TicksPerSecond / Math.Clamp(maximumHz, 1, 30);
        _item.Closed += OnItemClosed;
        _framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(_device, DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, _item.Size);
        _framePool.FrameArrived += OnFrameArrived;
        _session = _framePool.CreateCaptureSession(_item);
        _session.IsCursorCaptureEnabled = false;
        _session.StartCapture();
        return true;
    }

    private long MinimumFrameIntervalTicks { get; set; } = TimeSpan.TicksPerSecond / 8;

    public void Stop()
    {
        if (_item is not null) _item.Closed -= OnItemClosed;
        if (_framePool is not null) _framePool.FrameArrived -= OnFrameArrived;
        _session?.Dispose();
        _framePool?.Dispose();
        _session = null;
        _framePool = null;
        _item = null;
        Interlocked.Exchange(ref _processing, 0);
    }

    private void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
    {
        var now = DateTime.UtcNow.Ticks;
        if (now - Interlocked.Read(ref _lastFrameTicks) < MinimumFrameIntervalTicks || Interlocked.Exchange(ref _processing, 1) == 1)
        {
            FrameSkipped?.Invoke(this, EventArgs.Empty);
            using var skipped = sender.TryGetNextFrame();
            return;
        }

        try
        {
            using var frame = sender.TryGetNextFrame();
            if (frame is null) return;
            var size = frame.ContentSize;
            if (size.Width <= 0 || size.Height <= 0) return;
            var rect = _roi.ToPixels(size.Width, size.Height);
            using var bitmap = CanvasBitmap.CreateFromDirect3D11Surface(_device, frame.Surface);
            var bytes = bitmap.GetPixelBytes(rect.X, rect.Y, rect.Width, rect.Height);
            var pixels = new RgbColor[rect.Width * rect.Height];
            for (var i = 0; i < pixels.Length; i++)
            {
                var offset = i * 4;
                pixels[i] = new RgbColor(bytes[offset + 2], bytes[offset + 1], bytes[offset]);
            }
            Interlocked.Exchange(ref _lastFrameTicks, now);
            RoiFrameArrived?.Invoke(this, new PixelFrame(rect.Width, rect.Height, pixels));
        }
        catch (Exception exception) when (exception is ObjectDisposedException or COMException)
        {
            Stop();
            TargetClosed?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            Interlocked.Exchange(ref _processing, 0);
        }
    }

    private void OnItemClosed(GraphicsCaptureItem sender, object args)
    {
        Stop();
        TargetClosed?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        Stop();
        _device.Dispose();
    }
}
