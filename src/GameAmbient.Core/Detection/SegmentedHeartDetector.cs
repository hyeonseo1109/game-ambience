using GameAmbient.Core.Domain;

namespace GameAmbient.Core.Detection;

public sealed class SegmentedHeartDetector : IDetector
{
    private readonly SegmentedHeartDetectorOptions _options;

    public SegmentedHeartDetector(SegmentedHeartDetectorOptions options)
    {
        if (options.Slots.Count == 0 || options.FeatureGridSize is < 4 or > 24)
            throw new ArgumentException("Heart detector requires slots and a feature grid between 4 and 24.", nameof(options));
        if (options.Slots.Any(slot => !slot.Bounds.IsValid))
            throw new ArgumentException("Every heart slot must be inside the normalized ROI.", nameof(options));
        _options = options;
    }

    public string Kind => "segmented-heart";

    public DetectionResult Detect(PixelFrame frame, DateTimeOffset timestamp) => DetectDetailed(frame, timestamp).ToDetectionResult();

    public HeartDetectionResult DetectDetailed(PixelFrame frame, DateTimeOffset timestamp)
    {
        if (frame.Width <= 0 || frame.Height <= 0 || frame.Pixels.Length != frame.Width * frame.Height)
            return UnknownResult(timestamp);

        var results = new List<HeartSlotResult>(_options.Slots.Count);
        foreach (var slot in _options.Slots)
        {
            var pixels = Extract(frame, slot.Bounds);
            results.Add(Classify(slot.Index, pixels));
        }

        var full = results.Count(result => result.State == HeartState.Full);
        var half = results.Count(result => result.State == HeartState.Half);
        var empty = results.Count(result => result.State == HeartState.Empty);
        var unknown = results.Count - full - half - empty;
        var unknownFraction = unknown / (double)results.Count;
        var confidence = results.Count == 0 ? 0 : results.Average(result => result.Confidence) * (1 - unknownFraction);
        var valid = unknownFraction <= _options.MaximumUnknownFraction && confidence >= _options.MinimumSlotConfidence;
        var current = (full * 2) + half;
        var maximum = results.Count * 2;
        return new HeartDetectionResult(results, full, half, empty, unknown, current, maximum,
            valid ? current / (double)maximum : null, valid ? confidence : Math.Min(confidence, _options.MinimumSlotConfidence - .01), timestamp);
    }

    public static HeartTemplate CreateTemplate(PixelFrame slot, HeartState state, int gridSize = 8, string source = "user") =>
        new(state, gridSize, ExtractFeatures(slot, gridSize), source);

    private HeartSlotResult Classify(int index, PixelFrame slot)
    {
        var features = ExtractFeatures(slot, _options.FeatureGridSize);
        var redCoverage = CalculateRedCoverage(slot);
        if (_options.Templates.Count > 0)
        {
            var candidates = _options.Templates
                .Where(template => template.GridSize == _options.FeatureGridSize && template.Features.Count == features.Count)
                .Select(template => (template.State, Score: Similarity(features, template.Features)))
                .GroupBy(candidate => candidate.State)
                .Select(group => group.OrderByDescending(candidate => candidate.Score).First())
                .OrderByDescending(candidate => candidate.Score)
                .ToArray();
            if (candidates.Length > 0)
            {
                var best = candidates[0];
                var runnerUp = candidates.Length > 1 ? candidates[1].Score : .45;
                var margin = Math.Max(0, best.Score - runnerUp);
                var confidence = Math.Clamp(((best.Score - .55) / .35) * (.55 + Math.Min(.45, margin * 2.5)), 0, 1);
                var state = best.Score >= .68 && confidence >= _options.MinimumSlotConfidence ? best.State : HeartState.Unknown;
                return new HeartSlotResult(index, state, confidence, redCoverage, best.State, best.Score);
            }
        }
        return ClassifyByStructure(index, slot, redCoverage);
    }

    private static HeartSlotResult ClassifyByStructure(int index, PixelFrame slot, double redCoverage)
    {
        var left = RedCoverage(slot, 0, Math.Max(1, slot.Width / 2));
        var right = RedCoverage(slot, slot.Width / 2, slot.Width);
        var darkness = slot.Pixels.Span.ToArray().Count(pixel => (pixel.R + pixel.G + pixel.B) / 3d < 75) / (double)slot.Pixels.Length;
        HeartState state;
        double confidence;
        if (left >= .11 && right >= .11)
        {
            state = HeartState.Full;
            confidence = Math.Clamp(.62 + Math.Min(left, right), 0, 1);
        }
        else if ((left >= .11 && right < .085) || (right >= .11 && left < .085))
        {
            state = HeartState.Half;
            confidence = Math.Clamp(.60 + Math.Abs(left - right), 0, 1);
        }
        else if (redCoverage < .055 && darkness >= .06)
        {
            state = HeartState.Empty;
            confidence = Math.Clamp(.62 + darkness * .35, 0, 1);
        }
        else
        {
            state = HeartState.Unknown;
            confidence = Math.Clamp(.35 - Math.Abs(left - right) * .2, 0, .49);
        }
        return new HeartSlotResult(index, state, confidence, redCoverage, null, 0);
    }

    private static PixelFrame Extract(PixelFrame source, NormalizedRect normalized)
    {
        var rect = normalized.ToPixels(source.Width, source.Height);
        var pixels = new RgbColor[rect.Width * rect.Height];
        for (var y = 0; y < rect.Height; y++)
        for (var x = 0; x < rect.Width; x++)
            pixels[(y * rect.Width) + x] = source[rect.X + x, rect.Y + y];
        return new PixelFrame(rect.Width, rect.Height, pixels);
    }

    private static IReadOnlyList<double> ExtractFeatures(PixelFrame frame, int gridSize)
    {
        var features = new double[gridSize * gridSize * 2];
        for (var gy = 0; gy < gridSize; gy++)
        for (var gx = 0; gx < gridSize; gx++)
        {
            var x0 = gx * frame.Width / gridSize;
            var x1 = Math.Max(x0 + 1, (gx + 1) * frame.Width / gridSize);
            var y0 = gy * frame.Height / gridSize;
            var y1 = Math.Max(y0 + 1, (gy + 1) * frame.Height / gridSize);
            double red = 0, dark = 0;
            var count = 0;
            for (var y = y0; y < Math.Min(y1, frame.Height); y++)
            for (var x = x0; x < Math.Min(x1, frame.Width); x++)
            {
                var pixel = frame[x, y];
                red += Math.Max(0, pixel.R - Math.Max(pixel.G, pixel.B)) / 255d;
                dark += 1 - ((pixel.R + pixel.G + pixel.B) / (255d * 3));
                count++;
            }
            var offset = ((gy * gridSize) + gx) * 2;
            features[offset] = count == 0 ? 0 : red / count;
            features[offset + 1] = count == 0 ? 0 : dark / count;
        }
        return features;
    }

    private static double Similarity(IReadOnlyList<double> left, IReadOnlyList<double> right)
    {
        double sum = 0;
        for (var i = 0; i < left.Count; i += 2)
        {
            var red = left[i] - right[i];
            var dark = left[i + 1] - right[i + 1];
            sum += (red * red * .8) + (dark * dark * .2);
        }
        return Math.Clamp(1 - Math.Sqrt(sum / (left.Count / 2d)), 0, 1);
    }

    private static double CalculateRedCoverage(PixelFrame frame) => RedCoverage(frame, 0, frame.Width);

    private static double RedCoverage(PixelFrame frame, int startX, int endX)
    {
        var matches = 0;
        var total = Math.Max(1, (endX - startX) * frame.Height);
        for (var y = 0; y < frame.Height; y++)
        for (var x = startX; x < endX; x++)
        {
            var pixel = frame[x, y];
            if (pixel.R >= 90 && pixel.R >= pixel.G * 1.28 && pixel.R >= pixel.B * 1.20) matches++;
        }
        return matches / (double)total;
    }

    private HeartDetectionResult UnknownResult(DateTimeOffset timestamp) =>
        new([], 0, 0, 0, _options.Slots.Count, 0, _options.Slots.Count * 2, null, 0, timestamp);
}
