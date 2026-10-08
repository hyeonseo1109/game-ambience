using GameAmbient.Core.Analysis;
using GameAmbient.Core.Domain;

namespace GameAmbient.Core.Detection;

public sealed class ColorBarDetector(ColorBarDetectorOptions options) : IDetector
{
    public string Kind => "color-bar";

    public DetectionResult Detect(PixelFrame frame, DateTimeOffset timestamp)
    {
        if (frame.Width <= 0 || frame.Height <= 0 || frame.Pixels.Length != frame.Width * frame.Height)
            return DetectionResult.Unknown(timestamp);

        var horizontal = options.FillDirection is FillDirection.LeftToRight or FillDirection.RightToLeft;
        var axisLength = horizontal ? frame.Width : frame.Height;
        var crossLength = horizontal ? frame.Height : frame.Width;
        if (axisLength < 2 || crossLength < 1) return DetectionResult.Unknown(timestamp);

        var target = ColorMath.ToHsv(options.TargetColor);
        var scores = new double[axisLength];
        for (var axis = 0; axis < axisLength; axis++)
        {
            var matches = 0;
            for (var cross = 0; cross < crossLength; cross++)
            {
                var x = horizontal ? axis : cross;
                var y = horizontal ? cross : axis;
                var pixel = ColorMath.ToHsv(frame[x, y]);
                if (ColorMath.IsWithin(pixel, target, options.HueTolerance, options.SaturationTolerance, options.ValueTolerance)) matches++;
            }
            scores[axis] = matches / (double)crossLength;
        }

        var reverse = options.FillDirection is FillDirection.RightToLeft or FillDirection.BottomToTop;
        var ordered = reverse ? scores.Reverse().ToArray() : scores;
        var matchedLineCount = ordered.Count(score => score >= options.LineMatchThreshold);
        if (matchedLineCount == 0) return DetectionResult.Unknown(timestamp);

        var lastMatch = -1;
        var gap = 0;
        var gapsInside = 0;
        for (var i = 0; i < ordered.Length; i++)
        {
            if (ordered[i] >= options.LineMatchThreshold)
            {
                lastMatch = i;
                gapsInside += gap;
                gap = 0;
            }
            else if (++gap > options.MaxGap)
            {
                break;
            }
        }

        if (lastMatch < 0) return DetectionResult.Unknown(timestamp);
        var filledLines = lastMatch + 1;
        var value = filledLines / (double)axisLength;
        var interiorCoverage = ordered.Take(filledLines).Average();
        var continuity = 1 - (gapsInside / (double)Math.Max(1, filledLines));
        var outside = ordered.Skip(Math.Min(axisLength, filledLines + options.MaxGap)).ToArray();
        var boundaryContrast = outside.Length == 0 ? 1 : Math.Clamp(interiorCoverage - outside.Average(), 0, 1);
        var confidence = Math.Clamp((interiorCoverage * 0.5) + (continuity * 0.3) + (boundaryContrast * 0.2), 0, 1);

        return confidence < options.MinimumConfidence
            ? DetectionResult.Unknown(timestamp)
            : new DetectionResult(value, confidence, timestamp);
    }
}
