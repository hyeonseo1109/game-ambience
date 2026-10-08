using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using GameAmbient.Core.Domain;
using GameAmbient.Windows.Capture;

namespace GameAmbient.Windows.Overlay;

public partial class AmbientOverlayWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x00000020;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;
    private const int WsExLayered = 0x00080000;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;
    private static readonly IntPtr HwndTopmost = new(-1);
    private Storyboard? _pulse;
    private PixelBounds? _targetBounds;
    private double _intensity = 1;
    private double _widthScale = 1;
    private double _warningSeconds = 2;
    private double _criticalSeconds = 1.05;

    public AmbientOverlayWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => MakeClickThrough();
    }

    public void SetTargetBounds(PixelBounds bounds)
    {
        _targetBounds = bounds;
        if (IsVisible) ApplyTargetBounds();
    }

    public void Configure(double intensity, double widthScale, double warningSeconds, double criticalSeconds)
    {
        _intensity = Math.Clamp(intensity, .1, 2);
        _widthScale = Math.Clamp(widthScale, .25, 2.5);
        _warningSeconds = Math.Clamp(warningSeconds, 1.5, 4);
        _criticalSeconds = Math.Clamp(criticalSeconds, .8, 1.5);
    }

    public void Preview(HudState state, double value = .2)
    {
        if (state is HudState.Safe or HudState.Unknown)
        {
            _pulse?.Stop(this);
            Hide();
            return;
        }

        var critical = state == HudState.Critical;
        var color = critical ? System.Windows.Media.Color.FromRgb(255, 28, 34) : System.Windows.Media.Color.FromRgb(205, 35, 42);
        TopColor.Color = BottomColor.Color = LeftColor.Color = RightColor.Color = color;
        var danger = Math.Clamp((.30 - value) / .30, 0, 1);
        var peak = Math.Clamp((critical ? .44 : .22 + (.10 * danger)) * _intensity, .04, .70);
        var seconds = critical ? _criticalSeconds : _warningSeconds;
        var width = (critical ? 190 : 145) * _widthScale;
        TopGlow.Height = BottomGlow.Height = LeftGlow.Width = RightGlow.Width = width;

        if (_targetBounds is null)
        {
            Left = SystemParameters.VirtualScreenLeft;
            Top = SystemParameters.VirtualScreenTop;
            Width = SystemParameters.VirtualScreenWidth;
            Height = SystemParameters.VirtualScreenHeight;
        }
        if (!IsVisible) Show();
        ApplyTargetBounds();

        _pulse?.Stop(this);
        var animation = new DoubleAnimationUsingKeyFrames { RepeatBehavior = RepeatBehavior.Forever };
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(peak * .45, KeyTime.FromPercent(0)));
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(peak, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(seconds / 2))) { EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } });
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(peak * .45, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(seconds))) { EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } });
        _pulse = new Storyboard();
        _pulse.Children.Add(animation);
        Storyboard.SetTarget(animation, this);
        Storyboard.SetTargetProperty(animation, new PropertyPath(OpacityProperty));
        _pulse.Begin(this, true);
    }

    private void ApplyTargetBounds()
    {
        if (_targetBounds is not { } bounds) return;
        var handle = new WindowInteropHelper(this).Handle;
        SetWindowPos(handle, HwndTopmost, bounds.Left, bounds.Top, bounds.Width, bounds.Height, SwpNoActivate | SwpShowWindow);
    }

    private void MakeClickThrough()
    {
        var handle = new WindowInteropHelper(this).Handle;
        var style = GetWindowLongPtr(handle, GwlExStyle).ToInt64();
        SetWindowLongPtr(handle, GwlExStyle, new IntPtr(style | WsExTransparent | WsExToolWindow | WsExNoActivate | WsExLayered));
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value);
    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
}
