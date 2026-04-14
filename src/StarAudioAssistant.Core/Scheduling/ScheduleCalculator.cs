namespace StarAudioAssistant.Core.Scheduling;

public static class ScheduleCalculator
{
    public static DateTimeOffset? GetNextStart(ScheduleRule rule, DateTimeOffset now, Func<DateOnly, bool>? isHoliday = null)
    {
        if (!rule.Enabled)
        {
            return null;
        }

        var checker = isHoliday ?? (_ => false);

        if (rule.RecurrenceMode == TaskRecurrenceMode.OneTime)
        {
            if (!rule.StartDate.HasValue)
            {
                return null;
            }

            var absolute = BuildAbsoluteDateTime(rule.StartDate.Value, rule.StartTime, now.Offset);
            if (absolute < now)
            {
                return null;
            }

            return IsStartAllowed(rule, absolute, checker) ? absolute : null;
        }

        var candidate = GetNextBoundary(rule.StartDay, rule.StartTime, now);

        // Safeguard to avoid infinite loops when schedule mode never matches configured day.
        for (var i = 0; i < 370; i++)
        {
            if (IsStartAllowed(rule, candidate, checker))
            {
                return candidate;
            }

            candidate = candidate.AddDays(7);
        }

        return null;
    }

    public static IReadOnlyList<DateTimeOffset> GetUpcomingStarts(
        ScheduleRule rule,
        DateTimeOffset from,
        int count,
        Func<DateOnly, bool>? isHoliday = null)
    {
        var results = new List<DateTimeOffset>();
        if (count <= 0)
        {
            return results;
        }

        var cursor = from;
        for (var i = 0; i < count; i++)
        {
            var next = GetNextStart(rule, cursor, isHoliday);
            if (next is null)
            {
                break;
            }

            results.Add(next.Value);
            cursor = next.Value.AddSeconds(1);
        }

        return results;
    }

    public static DateTimeOffset GetEndBoundary(ScheduleRule rule, DateTimeOffset startBoundary)
    {
        if (rule.RecurrenceMode == TaskRecurrenceMode.OneTime && rule.EndDate.HasValue)
        {
            var absoluteEnd = BuildAbsoluteDateTime(rule.EndDate.Value, rule.EndTime, startBoundary.Offset);
            if (absoluteEnd > startBoundary)
            {
                return absoluteEnd;
            }

            return startBoundary.AddMinutes(1);
        }

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

    private static bool IsStartAllowed(ScheduleRule rule, DateTimeOffset candidate, Func<DateOnly, bool> isHoliday)
    {
        var date = DateOnly.FromDateTime(candidate.Date);

        if (rule.PauseUntilDate.HasValue && date <= rule.PauseUntilDate.Value)
        {
            return false;
        }

        if (rule.ScheduleMode == TaskScheduleMode.WeekdaysOnly &&
            (candidate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday))
        {
            return false;
        }

        if (rule.SkipOnHoliday && isHoliday(date))
        {
            return false;
        }

        return true;
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

    private static DateTimeOffset BuildAbsoluteDateTime(DateOnly date, TimeOnly time, TimeSpan offset) =>
        new(
            date.Year,
            date.Month,
            date.Day,
            time.Hour,
            time.Minute,
            time.Second,
            offset);
}
