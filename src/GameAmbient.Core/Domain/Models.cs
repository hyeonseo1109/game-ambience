namespace GameAmbient.Core.Domain;

public enum HudState { Unknown, Safe, Warning, Critical }
public enum FillDirection { LeftToRight, RightToLeft, TopToBottom, BottomToTop }
public enum CaptureTargetKind { Window, Monitor }
public enum PerformancePreset { LowPower, Balanced, Responsive }

public readonly record struct NormalizedRect(double X, double Y, double Width, double Height)
{
    public bool IsValid => X >= 0 && Y >= 0 && Width > 0 && Height > 0 && X + Width <= 1 && Y + Height <= 1;

    public PixelRect ToPixels(int width, int height)
    {
        if (!IsValid || width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width), "ROI and source dimensions must be valid.");
        var x = Math.Clamp((int)Math.Round(X * width), 0, width - 1);
        var y = Math.Clamp((int)Math.Round(Y * height), 0, height - 1);
        var right = Math.Clamp((int)Math.Round((X + Width) * width), x + 1, width);
        var bottom = Math.Clamp((int)Math.Round((Y + Height) * height), y + 1, height);
        return new PixelRect(x, y, right - x, bottom - y);
    }
}

public readonly record struct PixelRect(int X, int Y, int Width, int Height);
public readonly record struct RgbColor(byte R, byte G, byte B);
public readonly record struct HsvColor(double Hue, double Saturation, double Value);

public sealed record PixelFrame(int Width, int Height, ReadOnlyMemory<RgbColor> Pixels)
{
    public RgbColor this[int x, int y] => Pixels.Span[(y * Width) + x];
}

public sealed record DetectionResult(double? Value, double Confidence, DateTimeOffset Timestamp)
{
    public static DetectionResult Unknown(DateTimeOffset timestamp) => new(null, 0, timestamp);
}

public sealed record StabilizedSignal(double? Value, double Confidence, HudState State, DateTimeOffset Timestamp);
