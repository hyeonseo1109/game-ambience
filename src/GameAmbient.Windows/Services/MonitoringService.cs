using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using GameAmbient.Core.Detection;
using GameAmbient.Core.Domain;
using GameAmbient.Core.Pipeline;
using GameAmbient.Core.Profiles;
using GameAmbient.Core.Stabilization;
using GameAmbient.Windows.Capture;
using GameAmbient.Windows.Overlay;

namespace GameAmbient.Windows.Services;

public sealed record MonitoringSnapshot(
    MonitoringStatus Status,
    WindowTargetInfo? Target,
    double? RawValue,
    double? StableValue,
    double Confidence,
    HudState HudState,
    MetricsSnapshot Metrics,
    string Message);

public sealed class MonitoringService : IDisposable
{
    private readonly WindowsGraphicsCaptureSource _capture = new();
    private readonly AmbientOverlayWindow _overlay = new();
    private readonly MonitoringStateMachine _state = new();
    private readonly MonitoringMetrics _metrics = new();
    private readonly Stopwatch _uptime = new();
    private readonly DispatcherTimer _targetTimer;
    private IDetector _detector = new ColorBarDetector(new ColorBarDetectorOptions(new RgbColor(216, 52, 52)));
    private ISignalStabilizer _stabilizer = new MedianHysteresisStabilizer(new StabilizerOptions());
    private NormalizedRect? _roi;
    private StabilizedSignal? _latest;
    private bool _manualPause;
    private bool _previewActive;
    private bool _pauseOnFocusLoss = true;

    public MonitoringService()
    {
        _capture.RoiFrameArrived += OnFrameArrived;
        _capture.FrameSkipped += (_, _) => _metrics.RecordSkipped();
        _capture.TargetClosed += (_, _) => System.Windows.Application.Current.Dispatcher.BeginInvoke(TargetLost);
        _targetTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(250), DispatcherPriority.Background, (_, _) => TrackTarget(), System.Windows.Application.Current.Dispatcher);
    }

    public event EventHandler<MonitoringSnapshot>? SnapshotChanged;
    public MonitoringStatus Status => _state.Status;
    public WindowTargetInfo? Target => _capture.Target;

    public void Configure(ColorBarDetectorOptions detector, StabilizerOptions stabilizer, NormalizedRect roi)
    {
        _detector = new ColorBarDetector(detector);
        _stabilizer = new MedianHysteresisStabilizer(stabilizer);
        _roi = roi;
    }

    public void ConfigureApplication(ApplicationSettings settings)
    {
        _pauseOnFocusLoss = settings.PauseOnFocusLoss;
        _overlay.Configure(settings.OverlayIntensity, settings.GlowWidth, settings.WarningPulseSeconds, settings.CriticalPulseSeconds);
    }

    public async Task<WindowTargetInfo?> ChooseTargetAsync(IntPtr ownerWindow)
    {
        Stop();
        var target = await _capture.PickTargetAsync(ownerWindow);
        if (target is null) return null;
        _state.SelectTarget();
        UpdateBounds(target);
        Publish("Target ready. Calibrate an ROI, then start monitoring.");
        return target;
    }

    public bool Start()
    {
        if (_roi is not { } roi || !_state.Start()) return false;
        _manualPause = false;
        _previewActive = false;
        _stabilizer.Reset();
        _capture.Start(roi, 8);
        _uptime.Restart();
        _targetTimer.Start();
        Publish("Monitoring started. Switch to the game to activate detection.");
        return true;
    }

    public void Stop()
    {
        _capture.Stop();
        _targetTimer.Stop();
        _overlay.Preview(HudState.Safe);
        _previewActive = false;
        _manualPause = false;
        _state.Stop();
        Publish(_state.HasTarget ? "Monitoring stopped. Target remains selected." : "No target selected.");
    }

    public void TogglePause()
    {
        if (_state.Status == MonitoringStatus.Monitoring)
        {
            _manualPause = true;
            Pause("Monitoring paused.");
        }
        else if (_state.Status == MonitoringStatus.Paused)
        {
            _manualPause = false;
            ResumeIfPossible();
        }
    }

    public void Preview(HudState state, double value)
    {
        _previewActive = state is HudState.Warning or HudState.Critical;
        if (_capture.Target is { } target) UpdateBounds(target);
        _overlay.Preview(state, value);
        Publish(state == HudState.Safe ? "Overlay preview hidden." : $"{state} overlay preview. Input remains click-through.");
    }

    private void OnFrameArrived(object? sender, PixelFrame frame)
    {
        var watch = Stopwatch.StartNew();
        var raw = _detector.Detect(frame, DateTimeOffset.Now);
        var stable = _stabilizer.Push(raw);
        watch.Stop();
        _metrics.RecordProcessed(watch.Elapsed);
        _latest = stable;
        System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
        {
            if (!_previewActive) _overlay.Preview(stable.State, stable.Value ?? 1);
            Publish(stable.State == HudState.Unknown ? "Low confidence; previous warning was not escalated." : "Monitoring active.", raw.Value);
        });
    }

    private void TrackTarget()
    {
        var target = _capture.Target;
        if (target is null || !WindowTargetTracker.IsAlive(target))
        {
            TargetLost();
            return;
        }
        UpdateBounds(target);
        if (!_pauseOnFocusLoss || !target.HasWindowHandle || _previewActive) return;
        var foreground = WindowTargetTracker.IsForeground(target);
        if (!foreground && _state.Status == MonitoringStatus.Monitoring) Pause("Game is not foreground. Monitoring paused.");
        else if (foreground && _state.Status == MonitoringStatus.Paused && !_manualPause) ResumeIfPossible();
    }

    private void Pause(string message)
    {
        if (!_state.Pause()) return;
        _capture.SetPaused(true);
        _overlay.Preview(HudState.Safe);
        Publish(message);
    }

    private void ResumeIfPossible()
    {
        if (!_state.Resume()) return;
        _capture.SetPaused(false);
        if (_latest is { } latest) _overlay.Preview(latest.State, latest.Value ?? 1);
        Publish("Game foreground restored. Monitoring resumed.");
    }

    private void TargetLost()
    {
        _capture.ClearTarget();
        _targetTimer.Stop();
        _overlay.Preview(HudState.Safe);
        _state.LoseTarget();
        Publish("Target window is no longer available. Choose the game window again.");
    }

    private void UpdateBounds(WindowTargetInfo target)
    {
        if (WindowTargetTracker.TryGetBounds(target, out var bounds)) _overlay.SetTargetBounds(bounds);
    }

    private void Publish(string message, double? rawValue = null)
    {
        var stable = _latest;
        SnapshotChanged?.Invoke(this, new MonitoringSnapshot(_state.Status, _capture.Target, rawValue, stable?.Value,
            stable?.Confidence ?? 0, stable?.State ?? HudState.Unknown, _metrics.Snapshot(Math.Max(.001, _uptime.Elapsed.TotalSeconds)), message));
    }

    public void Dispose()
    {
        _targetTimer.Stop();
        _capture.Dispose();
        _overlay.Close();
    }
}
