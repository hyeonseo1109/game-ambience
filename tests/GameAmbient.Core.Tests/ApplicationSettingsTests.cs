using GameAmbient.Core.Profiles;

namespace GameAmbient.Core.Tests;

public sealed class ApplicationSettingsTests
{
    [Fact]
    public async Task Settings_round_trip()
    {
        var expected = new ApplicationSettings(OverlayIntensity: 1.3, GlowWidth: .8, LastProfilePath: "minecraft.gameambient.json");
        var store = new ApplicationSettingsStore();
        await using var stream = new MemoryStream();
        await store.SaveAsync(expected, stream);
        stream.Position = 0;
        Assert.Equal(expected, await store.LoadAsync(stream));
    }

    [Fact]
    public async Task Invalid_settings_fail_validation()
    {
        var store = new ApplicationSettingsStore();
        await using var stream = new MemoryStream();
        await Assert.ThrowsAsync<InvalidDataException>(() => store.SaveAsync(new ApplicationSettings(OverlayIntensity: 9), stream));
    }
}
