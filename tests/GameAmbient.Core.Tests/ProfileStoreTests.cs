using GameAmbient.Core.Detection;
using GameAmbient.Core.Domain;
using GameAmbient.Core.Profiles;
using GameAmbient.Core.Stabilization;

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

    private static GameProfile ValidProfile() => new(
        1, Guid.NewGuid(), "Demo", new CaptureTarget(CaptureTargetKind.Window, "game.exe", "Game", null),
        new NormalizedRect(.1, .8, .3, .05), new ColorBarDetectorOptions(new RgbColor(216, 52, 52)),
        new StabilizerOptions(), new AmbientEffectOptions(new RgbColor(180, 20, 25), new RgbColor(255, 25, 30)));
}
