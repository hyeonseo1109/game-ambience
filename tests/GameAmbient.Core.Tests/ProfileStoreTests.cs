using GameAmbient.Core.Detection;
using GameAmbient.Core.Domain;
using GameAmbient.Core.Profiles;
using GameAmbient.Core.Stabilization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace GameAmbient.Core.Tests;

public sealed class ProfileStoreTests
{
    [Fact]
    public async Task Round_trips_valid_profile()
    {
        var store = new ProfileStore();
        var profile = ValidProfile();
        await using var stream = new MemoryStream();
        await store.SaveAsync(profile, stream);
        stream.Position = 0;
        var loaded = await store.LoadAsync(stream);
        Assert.Equal(profile, loaded);
    }

    [Fact]
    public async Task Invalid_json_fails_gracefully()
    {
        var store = new ProfileStore();
        await using var stream = new MemoryStream("not json"u8.ToArray());
        await Assert.ThrowsAsync<InvalidDataException>(() => store.LoadAsync(stream));
    }

    [Fact]
    public async Task Segmented_profile_round_trips_and_creates_detector()
    {
        var frame = SyntheticHearts.Create(Enumerable.Repeat(HeartState.Full, 10).ToArray());
        var profile = ValidProfile() with
        {
            SchemaVersion = GameProfile.CurrentSchemaVersion,
            DetectorType = "segmented-heart",
            SegmentedHearts = new SegmentedHeartDetectorOptions(HeartSlotEstimator.Estimate(frame, 10), [])
        };
        await using var stream = new MemoryStream();
        var store = new ProfileStore();
        await store.SaveAsync(profile, stream);
        stream.Position = 0;
        var loaded = await store.LoadAsync(stream);
        Assert.IsType<SegmentedHeartDetector>(DetectorFactory.Create(loaded));
        Assert.Equal(10, loaded.SegmentedHearts!.Slots.Count);
    }

    [Fact]
    public async Task Version_one_profile_without_detector_type_remains_color_bar()
    {
        var node = JsonSerializer.SerializeToNode(ValidProfile())!.AsObject();
        node["schemaVersion"] = 1;
        node.Remove("detectorType");
        node.Remove("segmentedHearts");
        await using var stream = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(node));
        var loaded = await new ProfileStore().LoadAsync(stream);
        Assert.IsType<ColorBarDetector>(DetectorFactory.Create(loaded));
    }

    private static GameProfile ValidProfile() => new(
        1, Guid.NewGuid(), "Demo", new CaptureTarget(CaptureTargetKind.Window, "game.exe", "Game", null),
        new NormalizedRect(.1, .8, .3, .05), new ColorBarDetectorOptions(new RgbColor(216, 52, 52)),
        new StabilizerOptions(), new AmbientEffectOptions(new RgbColor(180, 20, 25), new RgbColor(255, 25, 30)));
}
