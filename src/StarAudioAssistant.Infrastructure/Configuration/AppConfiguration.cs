namespace StarAudioAssistant.Infrastructure.Configuration;

public sealed class AppConfiguration
{
    public List<ScheduledTaskConfiguration> Tasks { get; set; } = [];

    public List<DateOnly> HolidayDates { get; set; } = [];

    public UiConfiguration Ui { get; set; } = new();
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

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public int Priority { get; set; } = 100;

    public bool Enabled { get; set; } = true;

    public int FadeInMs { get; set; } = 1500;

    public int FadeOutMs { get; set; } = 1500;

    public int SortOrder { get; set; }

    public string RecurrenceMode { get; set; } = "Weekly";

    public string ScheduleMode { get; set; } = "EveryWeek";

    public bool SkipOnHoliday { get; set; }

    public DateOnly? PauseUntilDate { get; set; }
}

public sealed class UiConfiguration
{
    public string SortMode { get; set; } = "NextTrigger";

    public string QuickFilter { get; set; } = "All";

    public List<ColumnPreference> Columns { get; set; } = [];
}

public sealed class ColumnPreference
{
    public string Key { get; set; } = string.Empty;

    public bool IsVisible { get; set; } = true;

    public double Width { get; set; } = double.NaN;
}
