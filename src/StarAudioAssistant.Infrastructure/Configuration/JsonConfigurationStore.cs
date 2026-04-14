using System.Text.Json;
using System.Text.Json.Serialization;

namespace StarAudioAssistant.Infrastructure.Configuration;

public sealed class JsonConfigurationStore
{
    private const string CurrentAppFolderName = "StarmoAudioAssistant";
    private const string LegacyAppFolderName = "StarAudioAssistant";
    private readonly SemaphoreSlim _ioGate = new(1, 1);
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public JsonConfigurationStore(string? filePath = null)
    {
        if (!string.IsNullOrWhiteSpace(filePath))
        {
            FilePath = filePath;
            return;
        }

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var currentPath = Path.Combine(appData, CurrentAppFolderName, "config.json");
        TryMigrateLegacyConfig(appData, currentPath);
        FilePath = currentPath;
    }

    public string FilePath { get; }

    public async Task<AppConfiguration> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _ioGate.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(FilePath))
            {
                return new AppConfiguration();
            }

            try
            {
                var info = new FileInfo(FilePath);
                if (!info.Exists || info.Length == 0)
                {
                    return new AppConfiguration();
                }

                await using var stream = File.OpenRead(FilePath);
                var config = await JsonSerializer.DeserializeAsync<AppConfiguration>(stream, _serializerOptions, cancellationToken);
                return Normalize(config);
            }
            catch (JsonException)
            {
                BackupCorruptedFile();
                return new AppConfiguration();
            }
            catch (NotSupportedException)
            {
                BackupCorruptedFile();
                return new AppConfiguration();
            }
        }
        finally
        {
            _ioGate.Release();
        }
    }

    public async Task SaveAsync(AppConfiguration configuration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var sanitized = Normalize(configuration);
        var tempPath = $"{FilePath}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp";

        await _ioGate.WaitAsync(cancellationToken);
        try
        {
            try
            {
                await using (var stream = File.Create(tempPath))
                {
                    await JsonSerializer.SerializeAsync(stream, sanitized, _serializerOptions, cancellationToken);
                    await stream.FlushAsync(cancellationToken);
                }

                File.Move(tempPath, FilePath, overwrite: true);
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
        }
        finally
        {
            _ioGate.Release();
        }
    }

    private void BackupCorruptedFile()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return;
            }

            var backupPath = $"{FilePath}.broken-{DateTime.Now:yyyyMMdd-HHmmss}";
            File.Move(FilePath, backupPath, overwrite: true);
        }
        catch
        {
            // Ignore backup failures; load path will still recover with defaults.
        }
    }

    private static void TryMigrateLegacyConfig(string appData, string currentPath)
    {
        try
        {
            if (File.Exists(currentPath))
            {
                return;
            }

            var legacyPath = Path.Combine(appData, LegacyAppFolderName, "config.json");
            if (!File.Exists(legacyPath))
            {
                return;
            }

            var currentDirectory = Path.GetDirectoryName(currentPath);
            if (!string.IsNullOrWhiteSpace(currentDirectory))
            {
                Directory.CreateDirectory(currentDirectory);
            }

            File.Copy(legacyPath, currentPath, overwrite: false);
        }
        catch
        {
            // Ignore migration failures and continue with defaults.
        }
    }

    private static AppConfiguration Normalize(AppConfiguration? configuration)
    {
        var source = configuration ?? new AppConfiguration();
        var normalizedUi = source.Ui ?? new UiConfiguration();

        var columns = (normalizedUi.Columns ?? [])
            .Select(column => new ColumnPreference
            {
                Key = column.Key,
                IsVisible = column.IsVisible,
                Width = double.IsFinite(column.Width) ? column.Width : 0
            })
            .ToList();

        return new AppConfiguration
        {
            Tasks = source.Tasks ?? [],
            HolidayDates = source.HolidayDates ?? [],
            Ui = new UiConfiguration
            {
                SortMode = normalizedUi.SortMode,
                QuickFilter = normalizedUi.QuickFilter,
                Columns = columns
            }
        };
    }
}
