using GameAmbient.Core.Detection;
using GameAmbient.Core.Domain;

namespace GameAmbient.Core.Tests;

internal static class SyntheticHearts
{
    public static PixelFrame Create(IReadOnlyList<HeartState> states, int slotWidth = 16, int height = 14, byte red = 220, RgbColor? background = null)
    {
        var width = states.Count * slotWidth;
        var pixels = Enumerable.Repeat(background ?? new RgbColor(105, 110, 115), width * height).ToArray();
        for (var slot = 0; slot < states.Count; slot++) Draw(pixels, width, slot * slotWidth, slotWidth, height, states[slot], red);
        return new PixelFrame(width, height, pixels);
    }

    private static void Draw(RgbColor[] pixels, int frameWidth, int offsetX, int width, int height, HeartState state, byte red)
    {
        if (state == HeartState.Unknown) return;
        for (var y = 1; y < height - 1; y++)
        for (var x = 1; x < width - 1; x++)
        {
            var nx = x / (double)(width - 1);
            var ny = y / (double)(height - 1);
            var heart = ny < .38
                ? (Math.Pow(nx - .30, 2) + Math.Pow(ny - .26, 2) < .075 || Math.Pow(nx - .70, 2) + Math.Pow(ny - .26, 2) < .075)
                : Math.Abs(nx - .5) < .56 - (ny * .53);
            if (!heart) continue;
            var edge = x <= 2 || x >= width - 3 || y <= 2 || y >= height - 3 || Math.Abs(nx - .5) > .48 - (ny * .50);
            var fill = state == HeartState.Full || (state == HeartState.Half && nx <= .52);
            pixels[(y * frameWidth) + offsetX + x] = edge || !fill ? new RgbColor(28, 22, 24) : new RgbColor(red, 34, 42);
        }
    }
}
