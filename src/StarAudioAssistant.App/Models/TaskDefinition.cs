using System.IO;
using StarAudioAssistant.Core.Scheduling;

namespace StarAudioAssistant.App.Models;

public sealed class TaskDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string AudioPath { get; set; } = string.Empty;

    public DayOfWeek StartDay { get; set; }

    public TimeOnly StartTime { get; set; }

    public DayOfWeek EndDay { get; set; }

    public TimeOnly EndTime { get; set; }

    public int Priority { get; set; } = 100;

    public bool IsEnabled { get; set; } = true;

    public int FadeInMs { get; set; } = 1500;

    public int FadeOutMs { get; set; } = 1500;

    public int SortOrder { get; set; }

    public string RuntimeStatus { get; set; } = "等待中";

    public string TimeRange => $"{ToShortDay(StartDay)} {StartTime:HH\\:mm} -> {ToShortDay(EndDay)} {EndTime:HH\\:mm}";

    public string RuleType => StartDay == EndDay && EndTime > StartTime ? "每周循环" : "跨天循环";

    public string AudioFileName => Path.GetFileName(AudioPath);

    public string FadeDisplay => $"{FadeInMs / 1000d:0.0}s / {FadeOutMs / 1000d:0.0}s";

    public ScheduleRule ToScheduleRule() =>
        new(
            Name: Name,
            AudioPath: AudioPath,
            StartDay: StartDay,
            StartTime: StartTime,
            EndDay: EndDay,
            EndTime: EndTime,
            Priority: Priority,
            Enabled: IsEnabled);

    public TaskDefinition Clone() => new()
    {
        Id = Id,
        Name = Name,
        AudioPath = AudioPath,
        StartDay = StartDay,
        StartTime = StartTime,
        EndDay = EndDay,
        EndTime = EndTime,
        Priority = Priority,
        IsEnabled = IsEnabled,
        FadeInMs = FadeInMs,
        FadeOutMs = FadeOutMs,
        SortOrder = SortOrder,
        RuntimeStatus = RuntimeStatus
    };

    private static string ToShortDay(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "Mon",
        DayOfWeek.Tuesday => "Tue",
        DayOfWeek.Wednesday => "Wed",
        DayOfWeek.Thursday => "Thu",
        DayOfWeek.Friday => "Fri",
        DayOfWeek.Saturday => "Sat",
        DayOfWeek.Sunday => "Sun",
        _ => day.ToString()
    };
}
