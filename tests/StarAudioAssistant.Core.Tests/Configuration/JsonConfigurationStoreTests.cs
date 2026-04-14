using StarAudioAssistant.Infrastructure.Configuration;

namespace StarAudioAssistant.Core.Tests.Configuration;

public sealed class JsonConfigurationStoreTests : IDisposable
{
    private readonly string _root;

    public JsonConfigurationStoreTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "StarAudioAssistant.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public async Task LoadAsync_ReturnsDefault_WhenConfigFileIsEmpty()
    {
        var path = Path.Combine(_root, "config.json");
        await File.WriteAllTextAsync(path, string.Empty);

        var store = new JsonConfigurationStore(path);
        var config = await store.LoadAsync();

        Assert.NotNull(config);
        Assert.Empty(config.Tasks);
    }

    [Fact]
    public async Task SaveAsync_WritesNonEmptyFile_WhenColumnWidthContainsNaN()
    {
        var path = Path.Combine(_root, "config.json");
        var store = new JsonConfigurationStore(path);

        var config = new AppConfiguration
        {
            Ui = new UiConfiguration
            {
                Columns =
                [
                    new ColumnPreference
                    {
                        Key = "time",
                        IsVisible = true,
                        Width = double.NaN
                    }
                ]
            }
        };

        await store.SaveAsync(config);

        var file = new FileInfo(path);
        Assert.True(file.Exists);
        Assert.True(file.Length > 0);

        var loaded = await store.LoadAsync();
        Assert.NotNull(loaded);
    }

    [Fact]
    public async Task SaveAsync_DoesNotThrow_WhenCalledConcurrently()
    {
        var path = Path.Combine(_root, "config.json");
        var store = new JsonConfigurationStore(path);

        var tasks = Enumerable.Range(0, 20)
            .Select(index =>
            {
                var config = new AppConfiguration
                {
                    Tasks =
                    [
                        new ScheduledTaskConfiguration
                        {
                            Id = Guid.NewGuid(),
                            Name = $"Task-{index}",
                            AudioPath = @"C:\Windows\Media\Alarm01.wav",
                            StartDay = DayOfWeek.Monday,
                            EndDay = DayOfWeek.Monday,
                            StartTime = new TimeOnly(6, 0),
                            EndTime = new TimeOnly(7, 0)
                        }
                    ],
                    Ui = new UiConfiguration
                    {
                        QuickFilter = "All",
                        Columns =
                        [
                            new ColumnPreference
                            {
                                Key = "name",
                                IsVisible = true,
                                Width = 120 + index
                            }
                        ]
                    }
                };

                return Task.Run(() => store.SaveAsync(config));
            })
            .ToArray();

        await Task.WhenAll(tasks);

        var loaded = await store.LoadAsync();
        Assert.NotNull(loaded);
        Assert.NotNull(loaded.Ui);
        Assert.Single(loaded.Tasks);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
