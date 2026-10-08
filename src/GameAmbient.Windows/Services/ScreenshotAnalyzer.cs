using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GameAmbient.Core.Domain;

namespace GameAmbient.Windows.Services;

public static class ScreenshotAnalyzer
{
    public static BitmapSource Load(string path)
    {
        using var stream = File.OpenRead(path);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var bitmap = decoder.Frames[0];
        bitmap.Freeze();
        return bitmap;
    }

    public static PixelFrame Crop(BitmapSource source, NormalizedRect roi)
    {
        var rect = roi.ToPixels(source.PixelWidth, source.PixelHeight);
        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        var stride = converted.PixelWidth * 4;
        var all = new byte[stride * converted.PixelHeight];
        converted.CopyPixels(all, stride, 0);
        var pixels = new RgbColor[rect.Width * rect.Height];
        for (var y = 0; y < rect.Height; y++)
        for (var x = 0; x < rect.Width; x++)
        {
            var sourceIndex = ((rect.Y + y) * stride) + ((rect.X + x) * 4);
            pixels[(y * rect.Width) + x] = new RgbColor(all[sourceIndex + 2], all[sourceIndex + 1], all[sourceIndex]);
        }
        return new PixelFrame(rect.Width, rect.Height, pixels);
    }

    public static BitmapSource CreateMask(PixelFrame frame, RgbColor target, double hueTolerance, double saturationTolerance, double valueTolerance)
    {
        var bytes = new byte[frame.Width * frame.Height * 4];
        var targetHsv = Core.Analysis.ColorMath.ToHsv(target);
        for (var i = 0; i < frame.Pixels.Length; i++)
        {
            var match = Core.Analysis.ColorMath.IsWithin(Core.Analysis.ColorMath.ToHsv(frame.Pixels.Span[i]), targetHsv, hueTolerance, saturationTolerance, valueTolerance);
            var offset = i * 4;
            bytes[offset] = match ? (byte)95 : (byte)22;
            bytes[offset + 1] = match ? (byte)226 : (byte)25;
            bytes[offset + 2] = match ? (byte)255 : (byte)28;
            bytes[offset + 3] = 255;
        }
        var bitmap = BitmapSource.Create(frame.Width, frame.Height, 96, 96, PixelFormats.Bgra32, null, bytes, frame.Width * 4);
        bitmap.Freeze();
        return bitmap;
    }

    public static BitmapSource CreateSyntheticBar(double value, int width = 320, int height = 42)
    {
        var stride = width * 4;
        var bytes = new byte[stride * height];
        var fill = (int)Math.Round(Math.Clamp(value, 0, 1) * width);
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var border = y < 2 || y >= height - 2 || x < 2 || x >= width - 2;
            var filled = x < fill;
            var offset = (y * stride) + (x * 4);
            var color = border ? new RgbColor(65, 70, 76) : filled ? new RgbColor(216, 52, 52) : new RgbColor(22, 25, 30);
            bytes[offset] = color.B;
            bytes[offset + 1] = color.G;
            bytes[offset + 2] = color.R;
            bytes[offset + 3] = 255;
        }
        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, bytes, stride);
        bitmap.Freeze();
        return bitmap;
    }
}
