using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using StarAudioAssistant.App.Models;
using StarAudioAssistant.App.Services;
using StarAudioAssistant.Core.Scheduling;

namespace StarAudioAssistant.App;

public partial class TaskEditorWindow : Window
{
    private readonly TaskDefinition? _source;
    private readonly IReadOnlyList<TaskDefinition> _existingTasks;
    private readonly IReadOnlyCollection<DateOnly> _holidays;
    private readonly DispatcherTimer _refreshDebounceTimer;
    private ConflictSuggestion? _conflictSuggestion;

    public TaskEditorWindow(TaskDefinition? source, IReadOnlyList<TaskDefinition> existingTasks, IReadOnlyCollection<DateOnly> holidays)
    {
        _refreshDebounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(180)
        };
        _refreshDebounceTimer.Tick += RefreshDebounceTimer_Tick;
        InitializeComponent();

        _source = source;
        _existingTasks = existingTasks;
        _holidays = holidays;

        var days = Enum.GetValues<DayOfWeek>();
        StartDayComboBox.ItemsSource = days;
        EndDayComboBox.ItemsSource = days;

        if (source is null)
        {
            NameTextBox.Text = "新任务";
            StartDayComboBox.SelectedItem = DayOfWeek.Monday;
            EndDayComboBox.SelectedItem = DayOfWeek.Monday;
            StartTimeTextBox.Text = "06:00";
            EndTimeTextBox.Text = "08:00";

            var now = DateTime.Now;
            OneTimeStartDatePicker.SelectedDate = now.Date;
            OneTimeStartTimeTextBox.Text = now.ToString("HH:mm", CultureInfo.InvariantCulture);
            OneTimeEndDatePicker.SelectedDate = now.Date;
            OneTimeEndTimeTextBox.Text = now.AddHours(1).ToString("HH:mm", CultureInfo.InvariantCulture);

            PriorityTextBox.Text = "100";
            FadeInTextBox.Text = "1500";
            FadeOutTextBox.Text = "1500";
            EnabledCheckBox.IsChecked = true;
            SetScheduleMode(TaskScheduleMode.EveryWeek);
            SetRecurrenceMode(TaskRecurrenceMode.Weekly);
        }
        else
        {
            NameTextBox.Text = source.Name;
            AudioPathTextBox.Text = source.AudioPath;
            StartDayComboBox.SelectedItem = source.StartDay;
            EndDayComboBox.SelectedItem = source.EndDay;
            StartTimeTextBox.Text = source.StartTime.ToString("HH:mm", CultureInfo.InvariantCulture);
            EndTimeTextBox.Text = source.EndTime.ToString("HH:mm", CultureInfo.InvariantCulture);

            var startDate = source.StartDate ?? DateOnly.FromDateTime(DateTime.Now.Date);
            var endDate = source.EndDate ?? startDate;
            OneTimeStartDatePicker.SelectedDate = startDate.ToDateTime(TimeOnly.MinValue);
            OneTimeEndDatePicker.SelectedDate = endDate.ToDateTime(TimeOnly.MinValue);
            OneTimeStartTimeTextBox.Text = source.StartTime.ToString("HH:mm", CultureInfo.InvariantCulture);
            OneTimeEndTimeTextBox.Text = source.EndTime.ToString("HH:mm", CultureInfo.InvariantCulture);

            PriorityTextBox.Text = source.Priority.ToString(CultureInfo.InvariantCulture);
            FadeInTextBox.Text = source.FadeInMs.ToString(CultureInfo.InvariantCulture);
            FadeOutTextBox.Text = source.FadeOutMs.ToString(CultureInfo.InvariantCulture);
            EnabledCheckBox.IsChecked = source.IsEnabled;
            SkipHolidayCheckBox.IsChecked = source.SkipOnHoliday;
            SetScheduleMode(source.ScheduleMode);
            SetRecurrenceMode(source.RecurrenceMode);

            if (source.PauseUntilDate.HasValue)
            {
                PauseTaskCheckBox.IsChecked = true;
                PauseUntilDatePicker.IsEnabled = true;
                PauseUntilDatePicker.SelectedDate = source.PauseUntilDate.Value.ToDateTime(TimeOnly.MinValue);
            }
        }

        ApplyRecurrenceModeVisual(GetSelectedRecurrenceMode());

        StepTabControl.SelectedIndex = 0;
        UpdateWizardButtons();
        RefreshDerivedPanels();
    }

    public TaskDefinition? Result { get; private set; }

    protected override void OnClosed(EventArgs e)
    {
        _refreshDebounceTimer.Stop();
        _refreshDebounceTimer.Tick -= RefreshDebounceTimer_Tick;
        base.OnClosed(e);
    }

    private void StepTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        UpdateWizardButtons();
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (StepTabControl.SelectedIndex > 0)
        {
            StepTabControl.SelectedIndex -= 1;
        }
    }

    private void NextButton_Click(object sender, RoutedEventArgs e)
    {
        if (StepTabControl.SelectedIndex < StepTabControl.Items.Count - 1)
        {
            StepTabControl.SelectedIndex += 1;
        }
    }

    private void PauseTaskCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        PauseUntilDatePicker.IsEnabled = PauseTaskCheckBox.IsChecked == true;
        if (PauseTaskCheckBox.IsChecked != true)
        {
            PauseUntilDatePicker.SelectedDate = null;
        }

        RequestRefreshDerivedPanels();
    }

    private void RecurrenceModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        ApplyRecurrenceModeVisual(GetSelectedRecurrenceMode());
        RequestRefreshDerivedPanels();
    }

    private void AnyFieldChanged(object sender, EventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        RequestRefreshDerivedPanels();
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "音频文件 (*.mp3;*.wav)|*.mp3;*.wav|所有文件 (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) == true)
        {
            AudioPathTextBox.Text = dialog.FileName;
            RequestRefreshDerivedPanels();
        }
    }

    private void ApplyPrioritySuggestionButton_Click(object sender, RoutedEventArgs e)
    {
        if (_conflictSuggestion?.SuggestedPriority is not int suggested)
        {
            return;
        }

        PriorityTextBox.Text = suggested.ToString(CultureInfo.InvariantCulture);
        RequestRefreshDerivedPanels();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        ValidationTextBlock.Text = string.Empty;

        if (!TryBuildTask(strict: true, out var built, out var error))
        {
            ValidationTextBlock.Text = error;
            return;
        }

        Result = built;
        DialogResult = true;
        Close();
    }

    private void RefreshDebounceTimer_Tick(object? sender, EventArgs e)
    {
        _refreshDebounceTimer.Stop();
        RefreshDerivedPanels();
    }

    private void RequestRefreshDerivedPanels()
    {
        _refreshDebounceTimer.Stop();
        _refreshDebounceTimer.Start();
    }

    private void RefreshDerivedPanels()
    {
        ValidationTextBlock.Text = string.Empty;

        if (!TryBuildTask(strict: false, out var draft, out _))
        {
            ConflictSummaryText.Text = "请先填写基础字段以生成冲突检测。";
            ConflictAdviceText.Text = string.Empty;
            ApplyPrioritySuggestionButton.Visibility = Visibility.Collapsed;
            PreviewListBox.ItemsSource = null;
            HealthCheckText.Text = "待检测";
            HealthCheckText.Foreground = System.Windows.Media.Brushes.DarkSlateBlue;
            return;
        }

        var issue = TaskHealthService.GetHealthIssue(draft);
        HealthCheckText.Text = string.IsNullOrWhiteSpace(issue) ? "音频文件检查通过。" : issue;
        HealthCheckText.Foreground = string.IsNullOrWhiteSpace(issue)
            ? System.Windows.Media.Brushes.DarkSlateBlue
            : System.Windows.Media.Brushes.IndianRed;

        _conflictSuggestion = TaskConflictService.BuildSuggestion(draft, _existingTasks);
        if (_conflictSuggestion is null)
        {
            ConflictSummaryText.Text = "未发现启动冲突。";
            ConflictAdviceText.Text = string.Empty;
            ApplyPrioritySuggestionButton.Visibility = Visibility.Collapsed;
        }
        else
        {
            ConflictSummaryText.Text = _conflictSuggestion.Summary;
            ConflictAdviceText.Text = _conflictSuggestion.Recommendation;
            ApplyPrioritySuggestionButton.Visibility = _conflictSuggestion.SuggestedPriority.HasValue
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        var preview = TaskConflictService.BuildPreview(draft, DateTimeOffset.Now, _holidays, 7)
            .Select(dt => $"{dt:yyyy-MM-dd ddd HH:mm}")
            .ToList();

        PreviewListBox.ItemsSource = preview;
    }

    private bool TryBuildTask(bool strict, out TaskDefinition task, out string error)
    {
        task = new TaskDefinition();
        error = string.Empty;

        var name = NameTextBox.Text.Trim();
        if (strict && string.IsNullOrWhiteSpace(name))
        {
            error = "任务名称不能为空。";
            return false;
        }

        var audioPath = AudioPathTextBox.Text.Trim();
        if (strict)
        {
            if (string.IsNullOrWhiteSpace(audioPath) || !File.Exists(audioPath))
            {
                error = "请选择存在的音频文件。";
                return false;
            }
        }

        if (!int.TryParse(PriorityTextBox.Text.Trim(), out var priority) || priority < 0)
        {
            if (strict)
            {
                error = "优先级必须是大于等于 0 的整数。";
            }

            return false;
        }

        if (!int.TryParse(FadeInTextBox.Text.Trim(), out var fadeInMs) || fadeInMs < 0)
        {
            if (strict)
            {
                error = "淡入毫秒必须是大于等于 0 的整数。";
            }

            return false;
        }

        if (!int.TryParse(FadeOutTextBox.Text.Trim(), out var fadeOutMs) || fadeOutMs < 0)
        {
            if (strict)
            {
                error = "淡出毫秒必须是大于等于 0 的整数。";
            }

            return false;
        }

        var recurrenceMode = GetSelectedRecurrenceMode();
        var scheduleMode = GetSelectedScheduleMode();

        DayOfWeek startDay;
        DayOfWeek endDay;
        TimeOnly startTime;
        TimeOnly endTime;
        DateOnly? startDate = null;
        DateOnly? endDate = null;

        if (recurrenceMode == TaskRecurrenceMode.Weekly)
        {
            if (StartDayComboBox.SelectedItem is not DayOfWeek weeklyStartDay || EndDayComboBox.SelectedItem is not DayOfWeek weeklyEndDay)
            {
                if (strict)
                {
                    error = "请选择开始/结束星期。";
                }

                return false;
            }

            if (!TimeOnly.TryParseExact(StartTimeTextBox.Text.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out startTime))
            {
                if (strict)
                {
                    error = "开始时间格式应为 HH:mm。";
                }

                return false;
            }

            if (!TimeOnly.TryParseExact(EndTimeTextBox.Text.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out endTime))
            {
                if (strict)
                {
                    error = "结束时间格式应为 HH:mm。";
                }

                return false;
            }

            startDay = weeklyStartDay;
            endDay = weeklyEndDay;

            if (strict && scheduleMode == TaskScheduleMode.WeekdaysOnly)
            {
                if (startDay is DayOfWeek.Saturday or DayOfWeek.Sunday || endDay is DayOfWeek.Saturday or DayOfWeek.Sunday)
                {
                    error = "仅工作日模式下，开始/结束星期需为周一到周五。";
                    return false;
                }
            }
        }
        else
        {
            scheduleMode = TaskScheduleMode.EveryWeek;

            if (OneTimeStartDatePicker.SelectedDate is null || OneTimeEndDatePicker.SelectedDate is null)
            {
                if (strict)
                {
                    error = "单次任务请设置开始/结束日期。";
                }

                return false;
            }

            if (!TimeOnly.TryParseExact(OneTimeStartTimeTextBox.Text.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out startTime))
            {
                if (strict)
                {
                    error = "单次任务开始时间格式应为 HH:mm。";
                }

                return false;
            }

            if (!TimeOnly.TryParseExact(OneTimeEndTimeTextBox.Text.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out endTime))
            {
                if (strict)
                {
                    error = "单次任务结束时间格式应为 HH:mm。";
                }

                return false;
            }

            startDate = DateOnly.FromDateTime(OneTimeStartDatePicker.SelectedDate.Value.Date);
            endDate = DateOnly.FromDateTime(OneTimeEndDatePicker.SelectedDate.Value.Date);

            var startDateTime = startDate.Value.ToDateTime(startTime);
            var endDateTime = endDate.Value.ToDateTime(endTime);
            if (endDateTime <= startDateTime)
            {
                if (strict)
                {
                    error = "单次任务结束时间必须晚于开始时间。";
                }

                return false;
            }

            startDay = startDateTime.DayOfWeek;
            endDay = endDateTime.DayOfWeek;
        }

        DateOnly? pauseUntilDate = null;
        if (PauseTaskCheckBox.IsChecked == true)
        {
            if (PauseUntilDatePicker.SelectedDate is null)
            {
                if (strict)
                {
                    error = "请为临时停用选择结束日期。";
                }

                return false;
            }

            pauseUntilDate = DateOnly.FromDateTime(PauseUntilDatePicker.SelectedDate.Value.Date);
        }

        task = new TaskDefinition
        {
            Id = _source?.Id ?? Guid.NewGuid(),
            Name = string.IsNullOrWhiteSpace(name) ? "新任务" : name,
            AudioPath = audioPath,
            StartDay = startDay,
            StartTime = startTime,
            EndDay = endDay,
            EndTime = endTime,
            StartDate = startDate,
            EndDate = endDate,
            RecurrenceMode = recurrenceMode,
            Priority = priority,
            IsEnabled = EnabledCheckBox.IsChecked == true,
            FadeInMs = fadeInMs,
            FadeOutMs = fadeOutMs,
            SortOrder = _source?.SortOrder ?? 0,
            RuntimeStatus = "等待中",
            ScheduleMode = scheduleMode,
            SkipOnHoliday = SkipHolidayCheckBox.IsChecked == true && recurrenceMode == TaskRecurrenceMode.Weekly,
            PauseUntilDate = pauseUntilDate
        };

        return true;
    }

    private TaskScheduleMode GetSelectedScheduleMode()
    {
        if (ScheduleModeComboBox.SelectedItem is ComboBoxItem item &&
            Enum.TryParse<TaskScheduleMode>(item.Tag?.ToString(), out var mode))
        {
            return mode;
        }

        return TaskScheduleMode.EveryWeek;
    }

    private TaskRecurrenceMode GetSelectedRecurrenceMode()
    {
        if (RecurrenceModeComboBox.SelectedItem is ComboBoxItem item &&
            Enum.TryParse<TaskRecurrenceMode>(item.Tag?.ToString(), out var mode))
        {
            return mode;
        }

        return TaskRecurrenceMode.Weekly;
    }

    private void SetScheduleMode(TaskScheduleMode mode)
    {
        foreach (var item in ScheduleModeComboBox.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Tag?.ToString(), mode.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                ScheduleModeComboBox.SelectedItem = item;
                return;
            }
        }

        ScheduleModeComboBox.SelectedIndex = 0;
    }

    private void SetRecurrenceMode(TaskRecurrenceMode mode)
    {
        foreach (var item in RecurrenceModeComboBox.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Tag?.ToString(), mode.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                RecurrenceModeComboBox.SelectedItem = item;
                return;
            }
        }

        RecurrenceModeComboBox.SelectedIndex = 0;
    }

    private void ApplyRecurrenceModeVisual(TaskRecurrenceMode mode)
    {
        if (WeeklyPanel is null || OneTimePanel is null || ScheduleModeComboBox is null || SkipHolidayCheckBox is null)
        {
            return;
        }

        var isWeekly = mode == TaskRecurrenceMode.Weekly;
        WeeklyPanel.Visibility = isWeekly ? Visibility.Visible : Visibility.Collapsed;
        OneTimePanel.Visibility = isWeekly ? Visibility.Collapsed : Visibility.Visible;
        ScheduleModeComboBox.IsEnabled = isWeekly;
        SkipHolidayCheckBox.IsEnabled = isWeekly;
    }

    private void UpdateWizardButtons()
    {
        var idx = StepTabControl.SelectedIndex;
        var last = StepTabControl.Items.Count - 1;

        BackButton.IsEnabled = idx > 0;
        NextButton.IsEnabled = idx < last;
        SaveButton.Visibility = idx == last ? Visibility.Visible : Visibility.Collapsed;
    }
}
