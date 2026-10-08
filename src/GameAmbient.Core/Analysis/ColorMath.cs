using GameAmbient.Core.Domain;

namespace GameAmbient.Core.Analysis;

public static class ColorMath
{
    public static HsvColor ToHsv(RgbColor color)
    {
        var r = color.R / 255d;
        var g = color.G / 255d;
        var b = color.B / 255d;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;
        var hue = delta == 0 ? 0
            : max == r ? 60 * (((g - b) / delta) % 6)
            : max == g ? 60 * (((b - r) / delta) + 2)
            : 60 * (((r - g) / delta) + 4);
        if (hue < 0) hue += 360;
        return new HsvColor(hue, max == 0 ? 0 : delta / max, max);
    }

    public static bool IsWithin(HsvColor value, HsvColor target, double hueTolerance, double saturationTolerance, double valueTolerance)
    {
        var hueDistance = Math.Abs(value.Hue - target.Hue);
        hueDistance = Math.Min(hueDistance, 360 - hueDistance);
        return hueDistance <= hueTolerance
            && Math.Abs(value.Saturation - target.Saturation) <= saturationTolerance
            && Math.Abs(value.Value - target.Value) <= valueTolerance;
    }
}
