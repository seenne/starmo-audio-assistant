namespace StarAudioAssistant.Core.Scheduling;

public sealed record ScheduleRule(
    string Name,
    string AudioPath,
    DayOfWeek StartDay,
    TimeOnly StartTime,
    DayOfWeek EndDay,
    TimeOnly EndTime,
    int Priority,
    bool Enabled = true);
