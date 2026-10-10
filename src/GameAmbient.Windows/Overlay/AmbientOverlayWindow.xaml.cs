using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using GameAmbient.Core.Domain;
using GameAmbient.Core.Effects;
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
    private PulseSignature? _activePulse;

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
        var plan = HeartbeatEffectPlanner.Create(state, value, _intensity, _widthScale, _warningSeconds, _criticalSeconds);
        if (!plan.IsVisible)
        {
            _pulse?.Stop(this);
            _pulse = null;
            _activePulse = null;
            Opacity = 0;
            Hide();
            return;
        }

        var red = (byte)Math.Round(205 + (50 * plan.Danger));
        var green = (byte)Math.Round(38 - (18 * plan.Danger));
        var blue = (byte)Math.Round(44 - (12 * plan.Danger));
        var color = System.Windows.Media.Color.FromRgb(red, green, blue);
        TopColor.Color = BottomColor.Color = LeftColor.Color = RightColor.Color = color;
        TopGlow.Height = BottomGlow.Height = LeftGlow.Width = RightGlow.Width = plan.GlowWidth;

        if (_targetBounds is null)
        {
            Left = SystemParameters.VirtualScreenLeft;
            Top = SystemParameters.VirtualScreenTop;
            Width = SystemParameters.VirtualScreenWidth;
            Height = SystemParameters.VirtualScreenHeight;
        }
        if (!IsVisible) Show();
        ApplyTargetBounds();

        var signature = new PulseSignature(
            (int)Math.Round(plan.CycleSeconds * 20),
            (int)Math.Round(plan.PeakOpacity * 50));
        if (_activePulse == signature && _pulse is not null) return;

        _pulse?.Stop(this);
        Opacity = 0;
        var seconds = plan.CycleSeconds;
        var peak = plan.PeakOpacity;
        var animation = new DoubleAnimationUsingKeyFrames
        {
            Duration = TimeSpan.FromSeconds(seconds),
            RepeatBehavior = RepeatBehavior.Forever
        };
        animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromPercent(0)));
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(peak, KeyTime.FromPercent(.13)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromPercent(.25)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } });
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(peak * .72, KeyTime.FromPercent(.34)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromPercent(.47)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } });
        animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromPercent(1)));
        _pulse = new Storyboard();
        _pulse.Children.Add(animation);
        Storyboard.SetTarget(animation, this);
        Storyboard.SetTargetProperty(animation, new PropertyPath(OpacityProperty));
        _pulse.Begin(this, true);
        _activePulse = signature;
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

    private readonly record struct PulseSignature(int CycleBucket, int OpacityBucket);
}
