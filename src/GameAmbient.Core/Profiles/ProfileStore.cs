using System.Text.Json;

namespace GameAmbient.Core.Profiles;

public sealed class ProfileStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public ProfileValidationResult Validate(GameProfile profile)
    {
        var errors = new List<string>();
        if (profile.SchemaVersion != GameProfile.CurrentSchemaVersion) errors.Add($"Unsupported schema version {profile.SchemaVersion}.");
        if (profile.Id == Guid.Empty) errors.Add("Profile id is required.");
        if (string.IsNullOrWhiteSpace(profile.Name)) errors.Add("Profile name is required.");
        if (!profile.Roi.IsValid) errors.Add("ROI must be inside normalized capture bounds.");
        if (profile.Stabilizer.CriticalEnter >= profile.Stabilizer.WarningEnter) errors.Add("Critical threshold must be below warning threshold.");
        if (profile.Ambient.MaximumOpacity is < 0 or > 1) errors.Add("Maximum opacity must be between 0 and 1.");
        return new ProfileValidationResult(errors.Count == 0, errors);
    }

    public async Task SaveAsync(GameProfile profile, Stream destination, CancellationToken cancellationToken = default)
    {
        var validation = Validate(profile);
        if (!validation.IsValid) throw new InvalidDataException(string.Join(" ", validation.Errors));
        await JsonSerializer.SerializeAsync(destination, profile, JsonOptions, cancellationToken);
    }

    public async Task<GameProfile> LoadAsync(Stream source, CancellationToken cancellationToken = default)
    {
        GameProfile profile;
        try
        {
            profile = await JsonSerializer.DeserializeAsync<GameProfile>(source, JsonOptions, cancellationToken)
                ?? throw new InvalidDataException("Profile is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Profile JSON is malformed.", exception);
        }

        var validation = Validate(profile);
        if (!validation.IsValid) throw new InvalidDataException(string.Join(" ", validation.Errors));
        return profile;
    }
}
