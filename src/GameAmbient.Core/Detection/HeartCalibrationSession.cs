using GameAmbient.Core.Domain;

namespace GameAmbient.Core.Detection;

public sealed class HeartCalibrationSession
{
    private PixelFrame? _frame;
    private IReadOnlyList<HeartSlot> _slots = [];
    private readonly List<HeartTemplate> _templates = [];

    public IReadOnlyList<HeartSlot> Slots => _slots;
    public IReadOnlyList<HeartTemplate> Templates => _templates;
    public HeartDetectionResult? LastResult { get; private set; }

    public void SetFrame(PixelFrame frame, int? slotCount = null, int rows = 1)
    {
        _frame = frame;
        _slots = HeartSlotEstimator.Estimate(frame, slotCount, rows);
        Detect();
    }

    public void SetSlotCount(int count, int rows = 1)
    {
        if (_frame is null) return;
        _slots = HeartSlotEstimator.Estimate(_frame, count, rows);
        Detect();
    }

    public void RegisterTemplate(int slotIndex, HeartState state)
    {
        if (_frame is null || slotIndex < 0 || slotIndex >= _slots.Count) throw new ArgumentOutOfRangeException(nameof(slotIndex));
        var slotFrame = Crop(_frame, _slots[slotIndex].Bounds);
        _templates.RemoveAll(template => template.State == state);
        _templates.Add(SegmentedHeartDetector.CreateTemplate(slotFrame, state));
        Detect();
    }

    public SegmentedHeartDetectorOptions CreateOptions() => new(_slots, _templates.ToArray());

    public HeartDetectionResult? Detect()
    {
        if (_frame is null || _slots.Count == 0) return LastResult = null;
        return LastResult = new SegmentedHeartDetector(CreateOptions()).DetectDetailed(_frame, DateTimeOffset.Now);
    }

    public void Load(SegmentedHeartDetectorOptions options, PixelFrame? frame = null)
    {
        _frame = frame;
        _slots = options.Slots;
        _templates.Clear();
        _templates.AddRange(options.Templates);
        if (_frame is not null) Detect();
    }

    private static PixelFrame Crop(PixelFrame source, NormalizedRect roi)
    {
        var rect = roi.ToPixels(source.Width, source.Height);
        var pixels = new RgbColor[rect.Width * rect.Height];
        for (var y = 0; y < rect.Height; y++)
        for (var x = 0; x < rect.Width; x++) pixels[(y * rect.Width) + x] = source[rect.X + x, rect.Y + y];
        return new PixelFrame(rect.Width, rect.Height, pixels);
    }
}
