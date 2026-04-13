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

    public static DateTimeOffset GetEndBoundary(ScheduleRule rule, DateTimeOffset startBoundary)
    {
        var duration = GetWindowDuration(rule);
        return startBoundary.Add(duration);
    }

    public static bool IsBoundaryCrossed(DayOfWeek boundaryDay, TimeOnly boundaryTime, DateTimeOffset fromExclusive, DateTimeOffset toInclusive)
    {
        if (toInclusive <= fromExclusive)
        {
            return false;
        }

        var nextBoundary = GetNextBoundary(boundaryDay, boundaryTime, fromExclusive.AddTicks(1));
        return nextBoundary <= toInclusive;
    }

    public static DateTimeOffset GetNextBoundary(DayOfWeek boundaryDay, TimeOnly boundaryTime, DateTimeOffset now)
    {
        var daysAhead = ((int)boundaryDay - (int)now.DayOfWeek + 7) % 7;
        var candidateDate = now.Date.AddDays(daysAhead);
        var candidate = new DateTimeOffset(
            candidateDate.Year,
            candidateDate.Month,
            candidateDate.Day,
            boundaryTime.Hour,
            boundaryTime.Minute,
            boundaryTime.Second,
            now.Offset);

        if (candidate < now)
        {
            candidate = candidate.AddDays(7);
        }

        return candidate;
    }

    private static TimeSpan GetWindowDuration(ScheduleRule rule)
    {
        var startMinutes = ((int)rule.StartDay * 24 * 60) + (rule.StartTime.Hour * 60) + rule.StartTime.Minute;
        var endMinutes = ((int)rule.EndDay * 24 * 60) + (rule.EndTime.Hour * 60) + rule.EndTime.Minute;

        var durationMinutes = (endMinutes - startMinutes + (7 * 24 * 60)) % (7 * 24 * 60);
        if (durationMinutes == 0)
        {
            durationMinutes = 7 * 24 * 60;
        }

        return TimeSpan.FromMinutes(durationMinutes);
    }
}
