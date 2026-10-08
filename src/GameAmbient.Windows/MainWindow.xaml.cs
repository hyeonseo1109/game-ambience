using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using GameAmbient.Core.Detection;
using GameAmbient.Core.Domain;
using GameAmbient.Core.Stabilization;
using GameAmbient.Windows.Capture;
using GameAmbient.Windows.Overlay;
using GameAmbient.Windows.Services;
using Microsoft.Win32;

namespace GameAmbient.Windows;

public partial class MainWindow : Window
{
    private readonly RgbColor _targetColor = new(216, 52, 52);
    private readonly ColorBarDetector _detector;
    private readonly MedianHysteresisStabilizer _stabilizer;
    private readonly AmbientOverlayWindow _overlay = new();
    private readonly WindowsGraphicsCaptureSource _capture = new();
    private BitmapSource? _screenshot;
    private Point? _dragStart;
    private NormalizedRect? _selectedRoi;

    public MainWindow()
    {
        InitializeComponent();
        _detector = new ColorBarDetector(new ColorBarDetectorOptions(_targetColor));
        _stabilizer = new MedianHysteresisStabilizer(new StabilizerOptions());
        ImageSurface.Width = 640;
        ImageSurface.Height = 360;
        Loaded += (_, _) => RunSimulator();
        _capture.RoiFrameArrived += Capture_RoiFrameArrived;
        _capture.TargetClosed += (_, _) => Dispatcher.Invoke(() => StopMonitoring("Capture target closed. Waiting for reconnection."));
        Closed += (_, _) => { _capture.Dispose(); _overlay.Close(); };
    }

    private void ImportScreenshot_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff|All files|*.*", CheckFileExists = true };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            _screenshot = ScreenshotAnalyzer.Load(dialog.FileName);
            ScreenshotImage.Source = _screenshot;
            ImageSurface.Width = _screenshot.PixelWidth;
            ImageSurface.Height = _screenshot.PixelHeight;
            EmptyScreenshotText.Visibility = Visibility.Collapsed;
            RoiRectangle.Visibility = Visibility.Collapsed;
            DetectedValueText.Text = ConfidenceText.Text = "—";
            RoiText.Text = "Drag across the health bar to create a normalized ROI.";
        }
        catch (Exception exception) when (exception is IOException or NotSupportedException or FileFormatException)
        {
            MessageBox.Show(this, "The image could not be opened. Choose a valid PNG, JPEG, BMP, GIF, or TIFF file.", "Image error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ImageSurface_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_screenshot is null) return;
        _dragStart = e.GetPosition(ImageSurface);
        ImageSurface.CaptureMouse();
        RoiRectangle.Visibility = Visibility.Visible;
    }

    private void ImageSurface_MouseMove(object sender, MouseEventArgs e)
    {
        if (_dragStart is null || e.LeftButton != MouseButtonState.Pressed) return;
        DrawSelection(_dragStart.Value, e.GetPosition(ImageSurface));
    }

    private void ImageSurface_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragStart is null || _screenshot is null) return;
        var end = e.GetPosition(ImageSurface);
        var start = _dragStart.Value;
        _dragStart = null;
        ImageSurface.ReleaseMouseCapture();
        DrawSelection(start, end);
        var left = Math.Clamp(Math.Min(start.X, end.X), 0, ImageSurface.Width);
        var top = Math.Clamp(Math.Min(start.Y, end.Y), 0, ImageSurface.Height);
        var width = Math.Clamp(Math.Abs(end.X - start.X), 0, ImageSurface.Width - left);
        var height = Math.Clamp(Math.Abs(end.Y - start.Y), 0, ImageSurface.Height - top);
        if (width < 4 || height < 4) return;
        var roi = new NormalizedRect(left / ImageSurface.Width, top / ImageSurface.Height, width / ImageSurface.Width, height / ImageSurface.Height);
        _selectedRoi = roi;
        AnalyzeScreenshot(roi);
    }

    private void DrawSelection(Point start, Point end)
    {
        var left = Math.Min(start.X, end.X);
        var top = Math.Min(start.Y, end.Y);
        System.Windows.Controls.Canvas.SetLeft(RoiRectangle, left);
        System.Windows.Controls.Canvas.SetTop(RoiRectangle, top);
        RoiRectangle.Width = Math.Abs(end.X - start.X);
        RoiRectangle.Height = Math.Abs(end.Y - start.Y);
    }

    private void AnalyzeScreenshot(NormalizedRect roi)
    {
        if (_screenshot is null) return;
        var frame = ScreenshotAnalyzer.Crop(_screenshot, roi);
        var result = _detector.Detect(frame, DateTimeOffset.Now);
        MaskImage.Source = ScreenshotAnalyzer.CreateMask(frame, _targetColor, 18, .35, .35);
        DetectedValueText.Text = result.Value is null ? "Unknown" : $"{result.Value:P0}";
        ConfidenceText.Text = $"{result.Confidence:P0}";
        StatePill.Text = result.Value is null ? "UNKNOWN" : result.Value <= .15 ? "CRITICAL" : result.Value <= .30 ? "WARNING" : "SAFE";
        RoiText.Text = $"Normalized ROI  x {roi.X:F3} · y {roi.Y:F3} · w {roi.Width:F3} · h {roi.Height:F3}";
    }

    private void SimulatorSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsInitialized) return;
        RunSimulator();
    }

    private void RunSimulator()
    {
        var requested = SimulatorSlider.Value / 100;
        var bitmap = ScreenshotAnalyzer.CreateSyntheticBar(requested);
        SyntheticImage.Source = bitmap;
        SimulatorValueText.Text = $"{SimulatorSlider.Value:F0}%";
        var result = _detector.Detect(ScreenshotAnalyzer.Crop(bitmap, new NormalizedRect(0, 0, 1, 1)), DateTimeOffset.Now);
        var stable = _stabilizer.Push(result);
        StatePill.Text = stable.State.ToString().ToUpperInvariant();
    }

    private void PreviewSafe_Click(object sender, RoutedEventArgs e) => _overlay.Preview(HudState.Safe);
    private void PreviewWarning_Click(object sender, RoutedEventArgs e) => _overlay.Preview(HudState.Warning, .25);
    private void PreviewCritical_Click(object sender, RoutedEventArgs e) => _overlay.Preview(HudState.Critical, .10);

    private async void StartMonitoring_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedRoi is not { } roi)
        {
            MessageBox.Show(this, "Import a screenshot and select the HUD region before starting monitoring.", "ROI required", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            CaptureStatusText.Text = "Choose a game window (recommended) or monitor…";
            var started = await _capture.PickAndStartAsync(new WindowInteropHelper(this).Handle, roi, 8);
            CaptureStatusText.Text = started ? "Connected · analyzing ROI at up to 8 Hz" : "Target selection cancelled.";
            StartMonitoringButton.IsEnabled = !started;
            StopMonitoringButton.IsEnabled = started;
        }
        catch (Exception exception)
        {
            StopMonitoring($"Capture could not start: {exception.Message}");
        }
    }

    private void StopMonitoring_Click(object sender, RoutedEventArgs e) => StopMonitoring("Monitoring stopped.");

    private void StopMonitoring(string status)
    {
        _capture.Stop();
        _overlay.Preview(HudState.Safe);
        CaptureStatusText.Text = status;
        StartMonitoringButton.IsEnabled = true;
        StopMonitoringButton.IsEnabled = false;
    }

    private void Capture_RoiFrameArrived(object? sender, PixelFrame frame)
    {
        var result = _detector.Detect(frame, DateTimeOffset.Now);
        var stable = _stabilizer.Push(result);
        Dispatcher.BeginInvoke(() =>
        {
            DetectedValueText.Text = stable.Value is null ? "Unknown" : $"{stable.Value:P0}";
            ConfidenceText.Text = $"{stable.Confidence:P0}";
            StatePill.Text = stable.State.ToString().ToUpperInvariant();
            _overlay.Preview(stable.State, stable.Value ?? 1);
        });
    }
}
