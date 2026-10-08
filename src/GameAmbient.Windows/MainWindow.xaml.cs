using System.IO;
using System.Windows;
using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Controls;
using System.Windows.Media;
using GameAmbient.Core.Detection;
using GameAmbient.Core.Domain;
using GameAmbient.Core.Pipeline;
using GameAmbient.Core.Profiles;
using GameAmbient.Core.Stabilization;
using GameAmbient.Windows.Services;
using Microsoft.Win32;
using Point = System.Windows.Point;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using WpfRectangle = System.Windows.Shapes.Rectangle;
using WpfTextBlock = System.Windows.Controls.TextBlock;
using MediaColor = System.Windows.Media.Color;
using MediaBrushes = System.Windows.Media.Brushes;
using IoPath = System.IO.Path;

namespace GameAmbient.Windows;

public partial class MainWindow : Window
{
    private RgbColor _targetColor = new(216, 52, 52);
    private ColorBarDetector _detector;
    private MedianHysteresisStabilizer _stabilizer;
    private readonly MonitoringService _monitoring;
    private readonly TrayIconService _tray;
    private readonly ApplicationSettingsFile _settingsFile = new();
    private ApplicationSettings _settings = new();
    private BitmapSource? _screenshot;
    private Point? _dragStart;
    private NormalizedRect? _selectedRoi;
    private bool _exitRequested;
    private string? _currentProfileName;
    private string _detectorType = "color-bar";
    private HeartCalibrationSession? _heartCalibration;
    private bool _updatingHeartControls;

    public MainWindow()
    {
        InitializeComponent();
        _detector = new ColorBarDetector(new ColorBarDetectorOptions(_targetColor));
        _stabilizer = new MedianHysteresisStabilizer(new StabilizerOptions());
        _monitoring = new MonitoringService();
        _tray = new TrayIconService();
        ImageSurface.Width = 640;
        ImageSurface.Height = 360;
        Loaded += async (_, _) => await InitializeAsync();
        _monitoring.SnapshotChanged += Monitoring_SnapshotChanged;
        _tray.OpenRequested += (_, _) => Dispatcher.Invoke(ShowFromTray);
        _tray.PauseResumeRequested += (_, _) => Dispatcher.Invoke(_monitoring.TogglePause);
        _tray.StopRequested += (_, _) => Dispatcher.Invoke(_monitoring.Stop);
        _tray.ExitRequested += (_, _) => Dispatcher.Invoke(ExitApplication);
        Closing += MainWindow_Closing;
        StateChanged += (_, _) => { if (WindowState == WindowState.Minimized && _settings.MinimizeToTray) Hide(); };
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
        if (_detectorType == "segmented-heart")
        {
            AnalyzeHearts(frame, roi);
            return;
        }
        ClearSlotOverlay();
        _targetColor = ScreenshotAnalyzer.SuggestSignalColor(frame);
        _detector = new ColorBarDetector(new ColorBarDetectorOptions(_targetColor));
        _monitoring.Configure(new ColorBarDetectorOptions(_targetColor), new StabilizerOptions(), roi);
        StartMonitoringButton.IsEnabled = _monitoring.Target is not null;
        var result = _detector.Detect(frame, DateTimeOffset.Now);
        MaskImage.Source = ScreenshotAnalyzer.CreateMask(frame, _targetColor, 18, .35, .35);
        DetectedValueText.Text = result.Value is null ? "Unknown" : $"{result.Value:P0}";
        ConfidenceText.Text = $"{result.Confidence:P0}";
        StatePill.Text = result.Value is null ? "UNKNOWN" : result.Value <= .15 ? "CRITICAL" : result.Value <= .30 ? "WARNING" : "SAFE";
        ColorText.Text = $"Signal color  #{_targetColor.R:X2}{_targetColor.G:X2}{_targetColor.B:X2} · auto-calibrated";
        RoiText.Text = $"Normalized ROI  x {roi.X:F3} · y {roi.Y:F3} · w {roi.Width:F3} · h {roi.Height:F3}";
    }

    private void AnalyzeHearts(PixelFrame frame, NormalizedRect roi)
    {
        if (_heartCalibration is null)
        {
            _heartCalibration = new HeartCalibrationSession();
            var estimated = HeartSlotEstimator.Estimate(frame).Count;
            _updatingHeartControls = true;
            HeartCountSlider.Value = estimated;
            HeartCountText.Text = estimated.ToString();
            _updatingHeartControls = false;
        }
        var count = (int)Math.Round(HeartCountSlider.Value);
        _heartCalibration.SetFrame(frame, count);
        var result = _heartCalibration.LastResult;
        if (result is null) return;
        DrawHeartSlots(roi, _heartCalibration.Slots, result.Slots);
        SelectedHeartSlider.Maximum = Math.Max(1, _heartCalibration.Slots.Count);
        DetectedValueText.Text = result.NormalizedValue is null ? "Unknown" : $"{result.NormalizedValue:P0}";
        ConfidenceText.Text = $"{result.Confidence:P0}";
        StatePill.Text = result.NormalizedValue is null ? "UNKNOWN" : result.NormalizedValue <= .15 ? "CRITICAL" : result.NormalizedValue <= .30 ? "WARNING" : "SAFE";
        HeartResultText.Text = $"FULL {result.FullHearts} · HALF {result.HalfHearts} · EMPTY {result.EmptyHearts} · UNKNOWN {result.UnknownHearts}\nHP {result.CurrentHealth}/{result.MaximumHealth} · templates {_heartCalibration.Templates.Count}/3";
        ColorText.Text = "Slot features: redness + structure + optional templates";
        RoiText.Text = $"Normalized heart ROI  x {roi.X:F3} · y {roi.Y:F3} · w {roi.Width:F3} · h {roi.Height:F3}";
        MaskImage.Source = null;
        _monitoring.Configure(new SegmentedHeartDetector(_heartCalibration.CreateOptions()), new StabilizerOptions(), roi);
        StartMonitoringButton.IsEnabled = _monitoring.Target is not null;
    }

    private void DrawHeartSlots(NormalizedRect roi, IReadOnlyList<HeartSlot> slots, IReadOnlyList<HeartSlotResult>? results = null)
    {
        ClearSlotOverlay();
        RoiRectangle.Visibility = Visibility.Visible;
        Canvas.SetLeft(RoiRectangle, roi.X * ImageSurface.Width);
        Canvas.SetTop(RoiRectangle, roi.Y * ImageSurface.Height);
        RoiRectangle.Width = roi.Width * ImageSurface.Width;
        RoiRectangle.Height = roi.Height * ImageSurface.Height;
        foreach (var slot in slots)
        {
            var state = results?.FirstOrDefault(item => item.Index == slot.Index)?.State ?? HeartState.Unknown;
            var color = state switch
            {
                HeartState.Full => Colors.LimeGreen,
                HeartState.Half => Colors.Gold,
                HeartState.Empty => Colors.DeepSkyBlue,
                _ => Colors.OrangeRed
            };
            var rectangle = new WpfRectangle { Stroke = new SolidColorBrush(color), StrokeThickness = 2, Fill = MediaBrushes.Transparent };
            var left = (roi.X + (slot.Bounds.X * roi.Width)) * ImageSurface.Width;
            var top = (roi.Y + (slot.Bounds.Y * roi.Height)) * ImageSurface.Height;
            rectangle.Width = slot.Bounds.Width * roi.Width * ImageSurface.Width;
            rectangle.Height = slot.Bounds.Height * roi.Height * ImageSurface.Height;
            Canvas.SetLeft(rectangle, left);
            Canvas.SetTop(rectangle, top);
            SelectionCanvas.Children.Add(rectangle);
            var label = new WpfTextBlock { Text = (slot.Index + 1).ToString(), Foreground = MediaBrushes.White, Background = new SolidColorBrush(MediaColor.FromArgb(170, 0, 0, 0)), FontSize = 12 };
            Canvas.SetLeft(label, left + 2);
            Canvas.SetTop(label, top + 1);
            SelectionCanvas.Children.Add(label);
        }
    }

    private void ClearSlotOverlay()
    {
        SelectionCanvas.Children.Clear();
        SelectionCanvas.Children.Add(RoiRectangle);
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
        _monitoring.Preview(stable.State, stable.Value ?? 1);
    }

    private void PreviewSafe_Click(object sender, RoutedEventArgs e) => _monitoring.Preview(HudState.Safe, 1);
    private void PreviewWarning_Click(object sender, RoutedEventArgs e) => _monitoring.Preview(HudState.Warning, .25);
    private void PreviewCritical_Click(object sender, RoutedEventArgs e) => _monitoring.Preview(HudState.Critical, .10);

    private async void ChooseTarget_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ChooseTargetButton.IsEnabled = false;
            CaptureStatusText.Text = "Waiting for Windows capture picker…";
            var target = await _monitoring.ChooseTargetAsync(new WindowInteropHelper(this).Handle);
            if (target is null)
            {
                CaptureStatusText.Text = "Target selection cancelled.";
                return;
            }
            TargetText.Text = target.Title;
            TargetDetailsText.Text = $"{target.ProcessName} · {target.CaptureWidth} × {target.CaptureHeight}";
            StartMonitoringButton.IsEnabled = _selectedRoi is not null;
        }
        catch (Exception exception)
        {
            CaptureStatusText.Text = $"Target selection failed: {exception.Message}";
        }
        finally
        {
            ChooseTargetButton.IsEnabled = true;
        }
    }

    private void StartMonitoring_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedRoi is not { } roi)
        {
            MessageBox.Show(this, "Import a screenshot and select the HUD region before starting monitoring.", "ROI required", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _monitoring.Configure(GetActiveDetector(), new StabilizerOptions(), roi);
        if (!_monitoring.Start()) CaptureStatusText.Text = "Choose a target and valid ROI before starting.";
    }

    private void StopMonitoring_Click(object sender, RoutedEventArgs e) => _monitoring.Stop();

    private async void SaveProfile_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedRoi is not { } roi)
        {
            MessageBox.Show(this, "Select a HUD region before saving a profile.", "ROI required", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var dialog = new SaveFileDialog { Filter = "Game Ambience profile|*.gameambient.json", FileName = "health.gameambient.json" };
        if (dialog.ShowDialog(this) != true) return;
        var profile = new GameProfile(GameProfile.CurrentSchemaVersion, Guid.NewGuid(), IoPath.GetFileNameWithoutExtension(dialog.FileName),
            new GameAmbient.Core.Profiles.CaptureTarget(CaptureTargetKind.Window, _monitoring.Target?.ProcessName, _monitoring.Target?.Title, null), roi,
            new ColorBarDetectorOptions(_targetColor), new StabilizerOptions(),
            new AmbientEffectOptions(new RgbColor(205, 35, 42), new RgbColor(255, 28, 34)),
            Calibration: _screenshot is null ? null : new CalibrationMetadata(_screenshot.PixelWidth, _screenshot.PixelHeight, DateTimeOffset.Now),
            DetectorType: _detectorType,
            SegmentedHearts: _detectorType == "segmented-heart" ? _heartCalibration?.CreateOptions() : null);
        await using var stream = File.Create(dialog.FileName);
        await new ProfileStore().SaveAsync(profile, stream);
        _currentProfileName = profile.Name;
        CaptureStatusText.Text = $"Profile saved · {IoPath.GetFileName(dialog.FileName)}";
    }

    private async void LoadProfile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Game Ambience profile|*.gameambient.json", CheckFileExists = true };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            await using var stream = File.OpenRead(dialog.FileName);
            var profile = await new ProfileStore().LoadAsync(stream);
            _selectedRoi = profile.Roi;
            _targetColor = profile.Detector.TargetColor;
            _detector = new ColorBarDetector(profile.Detector);
            _stabilizer = new MedianHysteresisStabilizer(profile.Stabilizer);
            _detectorType = string.IsNullOrWhiteSpace(profile.DetectorType) ? "color-bar" : profile.DetectorType;
            DetectorTypeCombo.SelectedIndex = _detectorType == "segmented-heart" ? 1 : 0;
            if (profile.SegmentedHearts is not null)
            {
                _heartCalibration = new HeartCalibrationSession();
                var frame = _screenshot is null ? null : ScreenshotAnalyzer.Crop(_screenshot, profile.Roi);
                _heartCalibration.Load(profile.SegmentedHearts, frame);
                _updatingHeartControls = true;
                HeartCountSlider.Value = profile.SegmentedHearts.Slots.Count;
                _updatingHeartControls = false;
                DrawHeartSlots(profile.Roi, profile.SegmentedHearts.Slots, _heartCalibration.LastResult?.Slots);
            }
            _monitoring.Configure(DetectorFactory.Create(profile), profile.Stabilizer, profile.Roi);
            _currentProfileName = profile.Name;
            ColorText.Text = $"Signal color  #{_targetColor.R:X2}{_targetColor.G:X2}{_targetColor.B:X2} · profile";
            RoiText.Text = $"Loaded ROI  x {profile.Roi.X:F3} · y {profile.Roi.Y:F3} · w {profile.Roi.Width:F3} · h {profile.Roi.Height:F3}";
            CaptureStatusText.Text = $"Profile loaded · {profile.Name}";
        }
        catch (InvalidDataException exception)
        {
            MessageBox.Show(this, exception.Message, "Invalid profile", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task InitializeAsync()
    {
        _settings = await _settingsFile.LoadAsync();
        IntensitySlider.Value = _settings.OverlayIntensity * 100;
        GlowWidthSlider.Value = _settings.GlowWidth * 100;
        _monitoring.ConfigureApplication(_settings);
        RunSimulator();
    }

    private void Monitoring_SnapshotChanged(object? sender, MonitoringSnapshot snapshot)
    {
        Dispatcher.BeginInvoke(() =>
        {
            CaptureStatusText.Text = snapshot.Message;
            if (snapshot.Target is { } target)
            {
                TargetText.Text = target.Title;
                TargetDetailsText.Text = $"{target.ProcessName} · {target.CaptureWidth} × {target.CaptureHeight}";
            }
            if (snapshot.RawValue is { } raw) DetectedValueText.Text = $"{raw:P0}";
            if (snapshot.StableValue is not null) DetectedValueText.Text = $"{snapshot.StableValue:P0}";
            ConfidenceText.Text = snapshot.Confidence == 0 ? "—" : $"{snapshot.Confidence:P0}";
            StatePill.Text = snapshot.HudState.ToString().ToUpperInvariant();
            var active = snapshot.Status is MonitoringStatus.Monitoring or MonitoringStatus.Paused;
            StartMonitoringButton.IsEnabled = !active && snapshot.Target is not null && _selectedRoi is not null;
            StopMonitoringButton.IsEnabled = active;
            _tray.Update(snapshot.Status.ToString(), _currentProfileName);
        });
    }

    private async void OverlaySetting_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsInitialized || IntensityValueText is null || GlowWidthValueText is null) return;
        IntensityValueText.Text = $"{IntensitySlider.Value:F0}%";
        GlowWidthValueText.Text = $"{GlowWidthSlider.Value:F0}%";
        _settings = _settings with { OverlayIntensity = IntensitySlider.Value / 100, GlowWidth = GlowWidthSlider.Value / 100 };
        _monitoring.ConfigureApplication(_settings);
        await _settingsFile.SaveAsync(_settings);
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (_exitRequested) return;
        e.Cancel = true;
        Hide();
        _tray.Update(_monitoring.Status.ToString(), _currentProfileName);
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void ExitApplication()
    {
        _exitRequested = true;
        _monitoring.Dispose();
        _tray.Dispose();
        Close();
        System.Windows.Application.Current.Shutdown();
    }

    private IDetector GetActiveDetector()
    {
        if (_detectorType == "segmented-heart" && _heartCalibration is not null)
            return new SegmentedHeartDetector(_heartCalibration.CreateOptions());
        return _detector;
    }

    private void DetectorTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsInitialized || DetectorTypeCombo.SelectedItem is not ComboBoxItem item) return;
        _detectorType = item.Tag?.ToString() ?? "color-bar";
        HeartSettingsPanel.Visibility = _detectorType == "segmented-heart" ? Visibility.Visible : Visibility.Collapsed;
        if (_selectedRoi is { } roi && _screenshot is not null)
        {
            if (_detectorType == "segmented-heart" && _heartCalibration is null)
            {
                var frame = ScreenshotAnalyzer.Crop(_screenshot, roi);
                var estimated = HeartSlotEstimator.Estimate(frame).Count;
                _updatingHeartControls = true;
                HeartCountSlider.Value = estimated;
                _updatingHeartControls = false;
            }
            AnalyzeScreenshot(roi);
        }
    }

    private void HeartCountSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsInitialized || _updatingHeartControls || HeartCountText is null) return;
        HeartCountText.Text = Math.Round(HeartCountSlider.Value).ToString("F0");
        SelectedHeartSlider.Maximum = Math.Max(1, Math.Round(HeartCountSlider.Value));
        if (_detectorType == "segmented-heart" && _selectedRoi is { } roi && _screenshot is not null) AnalyzeScreenshot(roi);
    }

    private void SelectedHeartSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (SelectedHeartText is not null) SelectedHeartText.Text = Math.Round(SelectedHeartSlider.Value).ToString("F0");
    }

    private void MarkFull_Click(object sender, RoutedEventArgs e) => RegisterHeartTemplate(HeartState.Full);
    private void MarkHalf_Click(object sender, RoutedEventArgs e) => RegisterHeartTemplate(HeartState.Half);
    private void MarkEmpty_Click(object sender, RoutedEventArgs e) => RegisterHeartTemplate(HeartState.Empty);

    private void RegisterHeartTemplate(HeartState state)
    {
        if (_heartCalibration is null || _selectedRoi is not { } roi) return;
        var index = Math.Clamp((int)Math.Round(SelectedHeartSlider.Value) - 1, 0, _heartCalibration.Slots.Count - 1);
        _heartCalibration.RegisterTemplate(index, state);
        if (_screenshot is not null) AnalyzeHearts(ScreenshotAnalyzer.Crop(_screenshot, roi), roi);
    }
}
