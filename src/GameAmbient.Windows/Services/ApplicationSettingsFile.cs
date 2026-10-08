using System.IO;
using GameAmbient.Core.Profiles;

namespace GameAmbient.Windows.Services;

public sealed class ApplicationSettingsFile
{
    private readonly string _path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GameAmbient", "settings.json");
    private readonly ApplicationSettingsStore _store = new();

    public async Task<ApplicationSettings> LoadAsync()
    {
        if (!File.Exists(_path)) return new ApplicationSettings();
        try
        {
            await using var stream = File.OpenRead(_path);
            return await _store.LoadAsync(stream);
        }
        catch (InvalidDataException)
        {
            return new ApplicationSettings();
        }
    }

    public async Task SaveAsync(ApplicationSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        await using var stream = File.Create(_path);
        await _store.SaveAsync(settings, stream);
    }
}
