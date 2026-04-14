namespace StarAudioAssistant.Core.Scheduling;

public sealed record ScheduleRule(
    string Name,
    string AudioPath,
    DayOfWeek StartDay,
    TimeOnly StartTime,
    DayOfWeek EndDay,
    TimeOnly EndTime,
    int Priority,
    bool Enabled = true,
    TaskRecurrenceMode RecurrenceMode = TaskRecurrenceMode.Weekly,
    TaskScheduleMode ScheduleMode = TaskScheduleMode.EveryWeek,
    bool SkipOnHoliday = false,
    DateOnly? PauseUntilDate = null,
    DateOnly? StartDate = null,
    DateOnly? EndDate = null);
