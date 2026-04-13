namespace StarAudioAssistant.Core.Scheduling;

public static class ScheduleCalculator
{
    public static DateTimeOffset? GetNextStart(ScheduleRule rule, DateTimeOffset now)
    {
        if (!rule.Enabled)
        {
            return null;
        }

        var daysAhead = ((int)rule.StartDay - (int)now.DayOfWeek + 7) % 7;
        var candidateDate = now.Date.AddDays(daysAhead);
        var candidate = new DateTimeOffset(
            candidateDate.Year,
            candidateDate.Month,
            candidateDate.Day,
            rule.StartTime.Hour,
            rule.StartTime.Minute,
            0,
            now.Offset);

        if (candidate < now)
        {
            candidate = candidate.AddDays(7);
        }

        return candidate;
    }
}
