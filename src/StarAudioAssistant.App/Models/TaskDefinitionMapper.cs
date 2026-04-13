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
                Priority = task.Priority,
                IsEnabled = task.Enabled,
                FadeInMs = task.FadeInMs,
                FadeOutMs = task.FadeOutMs,
                SortOrder = task.SortOrder,
                RuntimeStatus = task.Enabled ? "等待中" : "已停用"
            })
            .ToList();
    }

    public static AppConfiguration ToConfiguration(IEnumerable<TaskDefinition> tasks)
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
                    Priority = task.Priority,
                    Enabled = task.IsEnabled,
                    FadeInMs = task.FadeInMs,
                    FadeOutMs = task.FadeOutMs,
                    SortOrder = task.SortOrder
                })
                .ToList()
        };
    }
}
