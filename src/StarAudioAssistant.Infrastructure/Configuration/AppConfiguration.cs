namespace StarAudioAssistant.Infrastructure.Configuration;

public sealed class AppConfiguration
{
    public List<ScheduledTaskConfiguration> Tasks { get; set; } = [];
}

public sealed class ScheduledTaskConfiguration
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string AudioPath { get; set; } = string.Empty;

    public DayOfWeek StartDay { get; set; }

    public TimeOnly StartTime { get; set; }

    public DayOfWeek EndDay { get; set; }

    public TimeOnly EndTime { get; set; }

    public int Priority { get; set; } = 100;

    public bool Enabled { get; set; } = true;

    public int FadeInMs { get; set; } = 1500;

    public int FadeOutMs { get; set; } = 1500;

    public int SortOrder { get; set; }
}
