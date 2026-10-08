using GameAmbient.Core.Detection;

namespace GameAmbient.Core.Profiles;

public static class DetectorFactory
{
    public static IDetector Create(GameProfile profile)
    {
        var type = string.IsNullOrWhiteSpace(profile.DetectorType) ? "color-bar" : profile.DetectorType;
        return type switch
        {
            "color-bar" => new ColorBarDetector(profile.Detector),
            "segmented-heart" when profile.SegmentedHearts is not null => new SegmentedHeartDetector(profile.SegmentedHearts),
            _ => throw new InvalidDataException($"Detector configuration '{type}' is incomplete.")
        };
    }
}
