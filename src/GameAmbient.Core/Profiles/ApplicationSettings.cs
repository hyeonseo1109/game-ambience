using System.Text.Json;

namespace GameAmbient.Core.Profiles;

public sealed record ApplicationSettings(
    bool MinimizeToTray = true,
    bool PauseOnFocusLoss = true,
    string CapturePreset = "Balanced",
    double OverlayIntensity = 1,
    double GlowWidth = 1,
    double WarningPulseSeconds = 2,
    double CriticalPulseSeconds = 1.05,
    string? LastProfilePath = null);

public sealed class ApplicationSettingsStore
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public async Task<ApplicationSettings> LoadAsync(Stream source, CancellationToken cancellationToken = default)
    {
        try
        {
            return await JsonSerializer.DeserializeAsync<ApplicationSettings>(source, Options, cancellationToken) ?? new ApplicationSettings();
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Application settings are malformed.", exception);
        }
    }

    public async Task SaveAsync(ApplicationSettings settings, Stream destination, CancellationToken cancellationToken = default)
    {
        if (settings.OverlayIntensity is < .1 or > 2 || settings.GlowWidth is < .25 or > 2.5)
            throw new InvalidDataException("Overlay settings are outside the supported range.");
        await JsonSerializer.SerializeAsync(destination, settings, Options, cancellationToken);
    }
}
