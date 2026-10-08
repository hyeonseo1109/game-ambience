using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using GameAmbient.Core.Domain;

namespace GameAmbient.Windows.Overlay;

public partial class AmbientOverlayWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x00000020;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;
    private Storyboard? _pulse;

    public AmbientOverlayWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => MakeClickThrough();
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
        var color = critical ? Color.FromRgb(255, 28, 34) : Color.FromRgb(205, 35, 42);
        TopColor.Color = BottomColor.Color = LeftColor.Color = RightColor.Color = color;
        var danger = Math.Clamp((.30 - value) / .30, 0, 1);
        var peak = critical ? .44 : .22 + (.10 * danger);
        var seconds = critical ? 1.05 : 2.0;
        var width = critical ? 190 : 145;
        TopGlow.Height = BottomGlow.Height = LeftGlow.Width = RightGlow.Width = width;

        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
        if (!IsVisible) Show();

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

    private void MakeClickThrough()
    {
        var handle = new WindowInteropHelper(this).Handle;
        var style = GetWindowLongPtr(handle, GwlExStyle).ToInt64();
        SetWindowLongPtr(handle, GwlExStyle, new IntPtr(style | WsExTransparent | WsExToolWindow | WsExNoActivate));
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value);
}
