using StarAudioAssistant.App.Models;
using StarAudioAssistant.Core.Scheduling;

namespace StarAudioAssistant.App.Services;

public static class TaskConflictService
{
    public static ConflictSuggestion? BuildSuggestion(TaskDefinition candidate, IEnumerable<TaskDefinition> existing)
    {
        var now = DateTimeOffset.Now;
        var candidateStart = ScheduleCalculator.GetNextStart(candidate.ToScheduleRule(), now);
        if (candidateStart is null)
        {
            return null;
        }

        foreach (var other in existing.Where(task => task.Id != candidate.Id && task.IsEnabled))
        {
            var otherStart = ScheduleCalculator.GetNextStart(other.ToScheduleRule(), now);
            if (otherStart is null)
            {
                continue;
            }

            if (Math.Abs((otherStart.Value - candidateStart.Value).TotalSeconds) > 30)
            {
                continue;
            }

            var recommendedPriority = Math.Max(candidate.Priority, other.Priority + 1);
            var message = $"检测到冲突：\"{candidate.Name}\" 与 \"{other.Name}\" 在 {candidateStart:MM-dd ddd HH:mm} 同时开始。";

            if (candidate.Priority <= other.Priority)
            {
                return new ConflictSuggestion(
                    message,
                    $"建议将 \"{candidate.Name}\" 优先级调整为 {recommendedPriority}，避免被立即抢占。",
                    recommendedPriority);
            }

            return new ConflictSuggestion(
                message,
                "当前优先级已高于冲突任务，无需调整。",
                null);
        }

        return null;
    }

    public static IReadOnlyList<DateTimeOffset> BuildPreview(
        TaskDefinition task,
        DateTimeOffset from,
        IReadOnlyCollection<DateOnly> holidays,
        int count = 7)
    {
        var rule = task.ToScheduleRule();
        return ScheduleCalculator.GetUpcomingStarts(rule, from, count, day => holidays.Contains(day));
    }

}

public sealed record ConflictSuggestion(string Summary, string Recommendation, int? SuggestedPriority);
