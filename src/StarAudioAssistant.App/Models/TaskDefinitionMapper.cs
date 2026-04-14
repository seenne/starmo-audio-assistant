using StarAudioAssistant.Core.Scheduling;
using StarAudioAssistant.Infrastructure.Configuration;

namespace StarAudioAssistant.App.Models;

public static class TaskDefinitionMapper
{
    public static List<TaskDefinition> ToTaskDefinitions(AppConfiguration config)
    {
        return config.Tasks
            .OrderBy(task => task.SortOrder)
            .Select(task => new TaskDefinition
            {
                Id = task.Id == Guid.Empty ? Guid.NewGuid() : task.Id,
                Name = task.Name,
                AudioPath = task.AudioPath,
                StartDay = task.StartDay,
                StartTime = task.StartTime,
                EndDay = task.EndDay,
                EndTime = task.EndTime,
                StartDate = task.StartDate,
                EndDate = task.EndDate,
                Priority = task.Priority,
                IsEnabled = task.Enabled,
                FadeInMs = task.FadeInMs,
                FadeOutMs = task.FadeOutMs,
                SortOrder = task.SortOrder,
                RecurrenceMode = ParseRecurrence(task.RecurrenceMode),
                ScheduleMode = ParseMode(task.ScheduleMode),
                SkipOnHoliday = task.SkipOnHoliday,
                PauseUntilDate = task.PauseUntilDate,
                RuntimeStatus = task.Enabled ? "等待中" : "已停用"
            })
            .ToList();
    }

    public static AppConfiguration ToConfiguration(IEnumerable<TaskDefinition> tasks, IReadOnlyList<DateOnly>? holidayDates = null, UiConfiguration? ui = null)
    {
        return new AppConfiguration
        {
            Tasks = tasks
                .OrderBy(task => task.SortOrder)
                .Select(task => new ScheduledTaskConfiguration
                {
                    Id = task.Id,
                    Name = task.Name,
                    AudioPath = task.AudioPath,
                    StartDay = task.StartDay,
                    StartTime = task.StartTime,
                    EndDay = task.EndDay,
                    EndTime = task.EndTime,
                    StartDate = task.StartDate,
                    EndDate = task.EndDate,
                    Priority = task.Priority,
                    Enabled = task.IsEnabled,
                    FadeInMs = task.FadeInMs,
                    FadeOutMs = task.FadeOutMs,
                    SortOrder = task.SortOrder,
                    RecurrenceMode = task.RecurrenceMode.ToString(),
                    ScheduleMode = task.ScheduleMode.ToString(),
                    SkipOnHoliday = task.SkipOnHoliday,
                    PauseUntilDate = task.PauseUntilDate
                })
                .ToList(),
            HolidayDates = holidayDates?.ToList() ?? [],
            Ui = ui ?? new UiConfiguration()
        };
    }

    private static TaskScheduleMode ParseMode(string? raw)
    {
        if (Enum.TryParse<TaskScheduleMode>(raw, ignoreCase: true, out var mode))
        {
            return mode;
        }

        return TaskScheduleMode.EveryWeek;
    }

    private static TaskRecurrenceMode ParseRecurrence(string? raw)
    {
        if (Enum.TryParse<TaskRecurrenceMode>(raw, ignoreCase: true, out var mode))
        {
            return mode;
        }

        return TaskRecurrenceMode.Weekly;
    }
}
