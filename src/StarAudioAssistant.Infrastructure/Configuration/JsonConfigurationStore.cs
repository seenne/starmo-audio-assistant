using System.Text.Json;
using System.Text.Json.Serialization;

namespace StarAudioAssistant.Infrastructure.Configuration;

public sealed class JsonConfigurationStore
{
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public JsonConfigurationStore(string? filePath = null)
    {
        FilePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "StarAudioAssistant",
            "config.json");
    }

    public string FilePath { get; }

    public async Task<AppConfiguration> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(FilePath))
        {
            return new AppConfiguration();
        }

        await using var stream = File.OpenRead(FilePath);
        var config = await JsonSerializer.DeserializeAsync<AppConfiguration>(stream, _serializerOptions, cancellationToken);
        return config ?? new AppConfiguration();
    }

    public async Task SaveAsync(AppConfiguration configuration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(FilePath);
        await JsonSerializer.SerializeAsync(stream, configuration, _serializerOptions, cancellationToken);
    }
}
