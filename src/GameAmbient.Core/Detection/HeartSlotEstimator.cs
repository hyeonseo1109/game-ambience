using GameAmbient.Core.Domain;

namespace GameAmbient.Core.Detection;

public static class HeartSlotEstimator
{
    public static IReadOnlyList<HeartSlot> Estimate(PixelFrame roi, int? slotCount = null, int rows = 1)
    {
        if (roi.Width < 2 || roi.Height < 2 || rows < 1) return [];
        var estimated = slotCount ?? (int)Math.Round(roi.Width / Math.Max(1, roi.Height * .86));
        var count = Math.Clamp(estimated, 1, 40);
        rows = Math.Clamp(rows, 1, Math.Min(4, count));
        var perRow = (int)Math.Ceiling(count / (double)rows);
        var slots = new List<HeartSlot>(count);
        for (var index = 0; index < count; index++)
        {
            var row = index / perRow;
            var column = index % perRow;
            slots.Add(new HeartSlot(index, new NormalizedRect(
                column / (double)perRow,
                row / (double)rows,
                1d / perRow,
                1d / rows)));
        }
        return slots;
    }
}
