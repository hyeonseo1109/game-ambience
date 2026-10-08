using GameAmbient.Core.Domain;

namespace GameAmbient.Core.Tests;

internal static class SyntheticBar
{
    public static PixelFrame Create(double fill, int width = 100, int height = 12, RgbColor? color = null, RgbColor? background = null)
    {
        var foreground = color ?? new RgbColor(216, 52, 52);
        var empty = background ?? new RgbColor(22, 25, 30);
        var pixels = new RgbColor[width * height];
        var filled = (int)Math.Round(width * fill);
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
            pixels[(y * width) + x] = x < filled ? foreground : empty;
        return new PixelFrame(width, height, pixels);
    }
}
