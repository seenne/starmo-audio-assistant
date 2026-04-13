using StarAudioAssistant.Core.Scheduling;

namespace StarAudioAssistant.Core.Tests.Scheduling;

public class ScheduleCalculatorTests
{
    [Fact]
    public void GetNextStart_ReturnsSameDayBoundary_WhenStartLaterToday()
    {
        var rule = CreateRule(DayOfWeek.Monday, new TimeOnly(6, 0));
        var now = At(2026, 4, 13, 5, 30); // Monday

        var next = ScheduleCalculator.GetNextStart(rule, now);

        Assert.Equal(At(2026, 4, 13, 6, 0), next);
    }

    [Fact]
    public void GetNextStart_ReturnsNow_WhenExactlyAtBoundary()
    {
        var rule = CreateRule(DayOfWeek.Monday, new TimeOnly(6, 0));
        var now = At(2026, 4, 13, 6, 0);

        var next = ScheduleCalculator.GetNextStart(rule, now);

        Assert.Equal(now, next);
    }

    [Fact]
    public void GetNextStart_ReturnsNextWeek_WhenTodayBoundaryAlreadyPassed()
    {
        var rule = CreateRule(DayOfWeek.Monday, new TimeOnly(6, 0));
        var now = At(2026, 4, 13, 8, 30);

        var next = ScheduleCalculator.GetNextStart(rule, now);

        Assert.Equal(At(2026, 4, 20, 6, 0), next);
    }

    [Fact]
    public void GetNextStart_HandlesCrossDayRuleByStartBoundary()
    {
        var rule = new ScheduleRule(
            Name: "周二深夜",
            AudioPath: "D:/audio/night_guard.mp3",
            StartDay: DayOfWeek.Tuesday,
            StartTime: new TimeOnly(23, 0),
            EndDay: DayOfWeek.Wednesday,
            EndTime: new TimeOnly(5, 0),
            Priority: 100,
            Enabled: true);

        var now = At(2026, 4, 13, 9, 0); // Monday

        var next = ScheduleCalculator.GetNextStart(rule, now);

        Assert.Equal(At(2026, 4, 14, 23, 0), next);
    }

    [Fact]
    public void GetNextStart_ReturnsNull_WhenRuleDisabled()
    {
        var rule = CreateRule(DayOfWeek.Monday, new TimeOnly(6, 0)) with { Enabled = false };

        var next = ScheduleCalculator.GetNextStart(rule, At(2026, 4, 13, 5, 30));

        Assert.Null(next);
    }

    [Fact]
    public void GetEndBoundary_ReturnsSameDayEnd_WhenEndAfterStart()
    {
        var rule = new ScheduleRule(
            Name: "白天任务",
            AudioPath: "D:/audio/day.mp3",
            StartDay: DayOfWeek.Monday,
            StartTime: new TimeOnly(6, 0),
            EndDay: DayOfWeek.Monday,
            EndTime: new TimeOnly(8, 0),
            Priority: 100,
            Enabled: true);

        var start = At(2026, 4, 13, 6, 0);

        var end = ScheduleCalculator.GetEndBoundary(rule, start);

        Assert.Equal(At(2026, 4, 13, 8, 0), end);
    }

    [Fact]
    public void GetEndBoundary_ReturnsNextDayEnd_WhenCrossDayWindow()
    {
        var rule = new ScheduleRule(
            Name: "夜间任务",
            AudioPath: "D:/audio/night.mp3",
            StartDay: DayOfWeek.Tuesday,
            StartTime: new TimeOnly(23, 0),
            EndDay: DayOfWeek.Wednesday,
            EndTime: new TimeOnly(5, 0),
            Priority: 100,
            Enabled: true);

        var start = At(2026, 4, 14, 23, 0);

        var end = ScheduleCalculator.GetEndBoundary(rule, start);

        Assert.Equal(At(2026, 4, 15, 5, 0), end);
    }

    [Fact]
    public void IsBoundaryCrossed_ReturnsTrue_WhenBoundaryWithinRange()
    {
        var from = At(2026, 4, 13, 22, 59);
        var to = At(2026, 4, 13, 23, 1);

        var crossed = ScheduleCalculator.IsBoundaryCrossed(DayOfWeek.Monday, new TimeOnly(23, 0), from, to);

        Assert.True(crossed);
    }

    [Fact]
    public void IsBoundaryCrossed_ReturnsFalse_WhenBoundaryOutsideRange()
    {
        var from = At(2026, 4, 13, 22, 0);
        var to = At(2026, 4, 13, 22, 30);

        var crossed = ScheduleCalculator.IsBoundaryCrossed(DayOfWeek.Monday, new TimeOnly(23, 0), from, to);

        Assert.False(crossed);
    }

    private static ScheduleRule CreateRule(DayOfWeek startDay, TimeOnly startTime) =>
        new(
            Name: "测试任务",
            AudioPath: "D:/audio/test.mp3",
            StartDay: startDay,
            StartTime: startTime,
            EndDay: startDay,
            EndTime: new TimeOnly((startTime.Hour + 1) % 24, startTime.Minute),
            Priority: 100,
            Enabled: true);

    private static DateTimeOffset At(int year, int month, int day, int hour, int minute) =>
        new(year, month, day, hour, minute, 0, TimeSpan.FromHours(8));
}
