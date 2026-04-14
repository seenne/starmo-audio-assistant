using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using StarAudioAssistant.Core.Scheduling;

namespace StarAudioAssistant.App.Models;

public sealed class TaskDefinition : INotifyPropertyChanged
{
    private Guid _id = Guid.NewGuid();
    private string _name = string.Empty;
    private string _audioPath = string.Empty;
    private DayOfWeek _startDay;
    private TimeOnly _startTime;
    private DayOfWeek _endDay;
    private TimeOnly _endTime;
    private DateOnly? _startDate;
    private DateOnly? _endDate;
    private TaskRecurrenceMode _recurrenceMode = TaskRecurrenceMode.Weekly;
    private int _priority = 100;
    private bool _isEnabled = true;
    private int _fadeInMs = 1500;
    private int _fadeOutMs = 1500;
    private int _sortOrder;
    private string _runtimeStatus = "等待中";
    private string _nextTriggerText = "待计算";
    private TaskScheduleMode _scheduleMode = TaskScheduleMode.EveryWeek;
    private bool _skipOnHoliday;
    private DateOnly? _pauseUntilDate;
    private string _healthIssue = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public Guid Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string AudioPath
    {
        get => _audioPath;
        set
        {
            if (!SetProperty(ref _audioPath, value))
            {
                return;
            }

            OnPropertyChanged(nameof(AudioFileName));
        }
    }

    public DayOfWeek StartDay
    {
        get => _startDay;
        set
        {
            if (!SetProperty(ref _startDay, value))
            {
                return;
            }

            OnPropertyChanged(nameof(TimeRange));
            OnPropertyChanged(nameof(RuleType));
        }
    }

    public TimeOnly StartTime
    {
        get => _startTime;
        set
        {
            if (!SetProperty(ref _startTime, value))
            {
                return;
            }

            OnPropertyChanged(nameof(TimeRange));
            OnPropertyChanged(nameof(RuleType));
        }
    }

    public DayOfWeek EndDay
    {
        get => _endDay;
        set
        {
            if (!SetProperty(ref _endDay, value))
            {
                return;
            }

            OnPropertyChanged(nameof(TimeRange));
            OnPropertyChanged(nameof(RuleType));
        }
    }

    public TimeOnly EndTime
    {
        get => _endTime;
        set
        {
            if (!SetProperty(ref _endTime, value))
            {
                return;
            }

            OnPropertyChanged(nameof(TimeRange));
            OnPropertyChanged(nameof(RuleType));
        }
    }

    public DateOnly? StartDate
    {
        get => _startDate;
        set
        {
            if (!SetProperty(ref _startDate, value))
            {
                return;
            }

            OnPropertyChanged(nameof(TimeRange));
            OnPropertyChanged(nameof(RuleType));
        }
    }

    public DateOnly? EndDate
    {
        get => _endDate;
        set
        {
            if (!SetProperty(ref _endDate, value))
            {
                return;
            }

            OnPropertyChanged(nameof(TimeRange));
            OnPropertyChanged(nameof(RuleType));
        }
    }

    public TaskRecurrenceMode RecurrenceMode
    {
        get => _recurrenceMode;
        set
        {
            if (!SetProperty(ref _recurrenceMode, value))
            {
                return;
            }

            OnPropertyChanged(nameof(TimeRange));
            OnPropertyChanged(nameof(RuleType));
            OnPropertyChanged(nameof(StrategyText));
        }
    }

    public int Priority
    {
        get => _priority;
        set => SetProperty(ref _priority, value);
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    public int FadeInMs
    {
        get => _fadeInMs;
        set
        {
            if (!SetProperty(ref _fadeInMs, value))
            {
                return;
            }

            OnPropertyChanged(nameof(FadeDisplay));
        }
    }

    public int FadeOutMs
    {
        get => _fadeOutMs;
        set
        {
            if (!SetProperty(ref _fadeOutMs, value))
            {
                return;
            }

            OnPropertyChanged(nameof(FadeDisplay));
        }
    }

    public int SortOrder
    {
        get => _sortOrder;
        set => SetProperty(ref _sortOrder, value);
    }

    public string RuntimeStatus
    {
        get => _runtimeStatus;
        set => SetProperty(ref _runtimeStatus, value);
    }

    public string NextTriggerText
    {
        get => _nextTriggerText;
        set => SetProperty(ref _nextTriggerText, value);
    }

    public TaskScheduleMode ScheduleMode
    {
        get => _scheduleMode;
        set
        {
            if (!SetProperty(ref _scheduleMode, value))
            {
                return;
            }

            OnPropertyChanged(nameof(StrategyText));
        }
    }

    public bool SkipOnHoliday
    {
        get => _skipOnHoliday;
        set
        {
            if (!SetProperty(ref _skipOnHoliday, value))
            {
                return;
            }

            OnPropertyChanged(nameof(StrategyText));
        }
    }

    public DateOnly? PauseUntilDate
    {
        get => _pauseUntilDate;
        set
        {
            if (!SetProperty(ref _pauseUntilDate, value))
            {
                return;
            }

            OnPropertyChanged(nameof(StrategyText));
        }
    }

    public string HealthIssue
    {
        get => _healthIssue;
        set
        {
            if (!SetProperty(ref _healthIssue, value))
            {
                return;
            }

            OnPropertyChanged(nameof(HasHealthIssue));
        }
    }

    public bool HasHealthIssue => !string.IsNullOrWhiteSpace(HealthIssue);

    public string TimeRange => RecurrenceMode == TaskRecurrenceMode.OneTime && StartDate.HasValue && EndDate.HasValue
        ? $"{StartDate:yyyy-MM-dd} {StartTime:HH\\:mm} -> {EndDate:yyyy-MM-dd} {EndTime:HH\\:mm}"
        : $"{ToShortDay(StartDay)} {StartTime:HH\\:mm} -> {ToShortDay(EndDay)} {EndTime:HH\\:mm}";

    public string RuleType => RecurrenceMode == TaskRecurrenceMode.OneTime
        ? "单次执行"
        : StartDay == EndDay && EndTime > StartTime ? "每周循环" : "跨天循环";

    public string AudioFileName => Path.GetFileName(AudioPath);

    public string FadeDisplay => $"{FadeInMs / 1000d:0.0}s / {FadeOutMs / 1000d:0.0}s";

    public string StrategyText
    {
        get
        {
            var parts = new List<string>
            {
                RecurrenceMode == TaskRecurrenceMode.OneTime
                    ? "单次执行"
                    : ScheduleMode == TaskScheduleMode.WeekdaysOnly ? "工作日" : "每周"
            };

            if (SkipOnHoliday)
            {
                parts.Add("节假日跳过");
            }

            if (PauseUntilDate.HasValue)
            {
                parts.Add($"停用至 {PauseUntilDate:yyyy-MM-dd}");
            }

            return string.Join(" · ", parts);
        }
    }

    public ScheduleRule ToScheduleRule() =>
        new(
            Name: Name,
            AudioPath: AudioPath,
            StartDay: StartDay,
            StartTime: StartTime,
            EndDay: EndDay,
            EndTime: EndTime,
            Priority: Priority,
            Enabled: IsEnabled,
            RecurrenceMode: RecurrenceMode,
            ScheduleMode: ScheduleMode,
            SkipOnHoliday: SkipOnHoliday,
            PauseUntilDate: PauseUntilDate,
            StartDate: StartDate,
            EndDate: EndDate);

    public TaskDefinition Clone() => new()
    {
        Id = Id,
        Name = Name,
        AudioPath = AudioPath,
        StartDay = StartDay,
        StartTime = StartTime,
        EndDay = EndDay,
        EndTime = EndTime,
        StartDate = StartDate,
        EndDate = EndDate,
        RecurrenceMode = RecurrenceMode,
        Priority = Priority,
        IsEnabled = IsEnabled,
        FadeInMs = FadeInMs,
        FadeOutMs = FadeOutMs,
        SortOrder = SortOrder,
        RuntimeStatus = RuntimeStatus,
        NextTriggerText = NextTriggerText,
        ScheduleMode = ScheduleMode,
        SkipOnHoliday = SkipOnHoliday,
        PauseUntilDate = PauseUntilDate,
        HealthIssue = HealthIssue
    };

    public void UpdateFrom(TaskDefinition source)
    {
        Name = source.Name;
        AudioPath = source.AudioPath;
        StartDay = source.StartDay;
        StartTime = source.StartTime;
        EndDay = source.EndDay;
        EndTime = source.EndTime;
        StartDate = source.StartDate;
        EndDate = source.EndDate;
        RecurrenceMode = source.RecurrenceMode;
        Priority = source.Priority;
        IsEnabled = source.IsEnabled;
        FadeInMs = source.FadeInMs;
        FadeOutMs = source.FadeOutMs;
        ScheduleMode = source.ScheduleMode;
        SkipOnHoliday = source.SkipOnHoliday;
        PauseUntilDate = source.PauseUntilDate;
    }

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

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged(string? propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
