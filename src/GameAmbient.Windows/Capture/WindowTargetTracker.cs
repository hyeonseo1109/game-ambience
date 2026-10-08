using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace GameAmbient.Windows.Capture;

public readonly record struct PixelBounds(int Left, int Top, int Width, int Height);

public sealed record WindowTargetInfo(IntPtr Handle, string Title, string ProcessName, int CaptureWidth, int CaptureHeight)
{
    public bool HasWindowHandle => Handle != IntPtr.Zero;
}

public static class WindowTargetTracker
{
    private const int DwmwaExtendedFrameBounds = 9;

    public static WindowTargetInfo Resolve(string displayName, int captureWidth, int captureHeight)
    {
        var matches = new List<WindowTargetInfo>();
        EnumWindows((handle, _) =>
        {
            if (!IsWindowVisible(handle)) return true;
            var length = GetWindowTextLength(handle);
            if (length <= 0) return true;
            var title = new StringBuilder(length + 1);
            GetWindowText(handle, title, title.Capacity);
            var value = title.ToString();
            if (!string.Equals(value, displayName, StringComparison.OrdinalIgnoreCase)) return true;
            GetWindowThreadProcessId(handle, out var processId);
            var processName = "Unknown";
            try { processName = Process.GetProcessById((int)processId).ProcessName; } catch (ArgumentException) { }
            matches.Add(new WindowTargetInfo(handle, value, processName, captureWidth, captureHeight));
            return true;
        }, IntPtr.Zero);
        return matches.FirstOrDefault() ?? new WindowTargetInfo(IntPtr.Zero, displayName, "Monitor or protected window", captureWidth, captureHeight);
    }

    public static bool IsForeground(WindowTargetInfo target) => target.HasWindowHandle && GetForegroundWindow() == target.Handle;
    public static bool IsAlive(WindowTargetInfo target) => !target.HasWindowHandle || IsWindow(target.Handle);

    public static bool TryGetBounds(WindowTargetInfo target, out PixelBounds bounds)
    {
        bounds = default;
        if (!target.HasWindowHandle) return false;
        RECT rect;
        var result = DwmGetWindowAttribute(target.Handle, DwmwaExtendedFrameBounds, out rect, Marshal.SizeOf<RECT>());
        if (result != 0 && !GetWindowRect(target.Handle, out rect)) return false;
        if (rect.Right <= rect.Left || rect.Bottom <= rect.Top) return false;
        bounds = new PixelBounds(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
        return true;
    }

    private delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);
    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr window);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr window, StringBuilder text, int maxCount);
    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr window);
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr window, out RECT rect);
    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out RECT value, int size);
}
