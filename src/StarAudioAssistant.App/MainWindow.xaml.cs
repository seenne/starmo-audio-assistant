using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Threading;
using StarAudioAssistant.App.Models;
using StarAudioAssistant.App.Services;
using StarAudioAssistant.Audio.Playback;
using StarAudioAssistant.Core.Scheduling;
using StarAudioAssistant.Infrastructure.Configuration;
using MessageBox = System.Windows.MessageBox;

namespace StarAudioAssistant.App;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<TaskDefinition> _taskRows = [];
    private readonly JsonConfigurationStore _configStore = new();
    private readonly ErrorCenterService _errorCenter = new();
    private readonly SchedulerOrchestrator _scheduler;
    private readonly NotifyIcon _notifyIcon;
    private readonly ICollectionView _taskView;
    private readonly SemaphoreSlim _persistGate = new(1, 1);
    private readonly DispatcherTimer _uiPersistDebounceTimer;

    private List<DateOnly> _holidayDates = [];
    private UiConfiguration _uiConfig = new();
    private bool _allowClose;
    private bool _suppressUiEvents;
    private bool _uiPersistPending;
    private Guid? _currentPlayingTaskId;
    private string? _lastSchedulerError;
    private Guid? _lastFilterPlayingTaskId;

    public MainWindow()
    {
        InitializeComponent();

        TaskGrid.ItemsSource = _taskRows;
        _taskView = CollectionViewSource.GetDefaultView(_taskRows);
        _taskView.Filter = FilterTask;

        QuickFilterComboBox.ItemsSource = new List<FilterOption>
        {
            new(QuickFilterMode.All, "全部任务"),
            new(QuickFilterMode.Enabled, "仅启用"),
            new(QuickFilterMode.Playing, "仅播放中"),
            new(QuickFilterMode.Issue, "仅健康异常")
        };
        QuickFilterComboBox.SelectedIndex = 0;

        ToggleStrategyColumn.IsChecked = true;
        ToggleNextColumn.IsChecked = true;
        ToggleHealthColumn.IsChecked = false;

        _scheduler = new SchedulerOrchestrator(GetTaskSnapshot, new NaudioPlaybackService(), IsHolidayDate);
        _scheduler.SnapshotUpdated += OnSchedulerSnapshotUpdated;

        _notifyIcon = new NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Information,
            Visible = true,
            Text = "星晨音频助手"
        };

        _notifyIcon.DoubleClick += (_, _) => RestoreFromTray();
        _notifyIcon.ContextMenuStrip = BuildTrayMenu();

        _uiPersistDebounceTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(320)
        };
        _uiPersistDebounceTimer.Tick += UiPersistDebounceTimer_Tick;

        UpdateActionButtonsState();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var config = await _configStore.LoadAsync();
            var loaded = TaskDefinitionMapper.ToTaskDefinitions(config);

            _holidayDates = config.HolidayDates.Distinct().OrderBy(d => d).ToList();
            _uiConfig = config.Ui ?? new UiConfiguration();

            ReplaceTasks(loaded);
            RecomputeHealthIssues(logIssues: false);
            ApplyUiFromConfig();
            UpdateTaskRuntimeAndNextTrigger(DateTimeOffset.Now, currentTaskId: null);
            UpdateDashboardCards();
            UpdateSelectedTaskDetail();

            await PersistSafelyAsync("启动初始化");
            _scheduler.Start();
        }
        catch (Exception ex)
        {
            _errorCenter.Report("启动", "读取配置失败", ex.ToString());
            MessageBox.Show(this, $"启动失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }

        try
        {
            _uiPersistDebounceTimer.Stop();
            _uiPersistPending = false;
            await PersistCoreAsync();
        }
        catch
        {
            // Ignore persistence errors during shutdown.
        }

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _uiPersistDebounceTimer.Tick -= UiPersistDebounceTimer_Tick;
        _persistGate.Dispose();
        await _scheduler.DisposeAsync();
    }

    private async void AddTaskButton_Click(object sender, RoutedEventArgs e)
    {
        var editor = new TaskEditorWindow(null, GetTaskSnapshot(), _holidayDates) { Owner = this };
        TaskDefinition? result = null;
        if (!ShowDialogWithSchedulerPaused(editor, out result) || result is null)
        {
            return;
        }

        result.SortOrder = _taskRows.Count;
        result.RuntimeStatus = result.IsEnabled ? "等待中" : "已停用";
        _taskRows.Add(result);

        NormalizeOrder();
        RecomputeHealthIssues(logIssues: true);
        UpdateTaskRuntimeAndNextTrigger(DateTimeOffset.Now, currentTaskId: null);
        _taskView.Refresh();
        UpdateDashboardCards();
        SelectTaskById(result.Id);

        UpdateActionButtonsState();
        await PersistSafelyAsync("新增任务");
        _scheduler.Refresh();
    }

    private async void EditTaskButton_Click(object sender, RoutedEventArgs e)
    {
        if (TaskGrid.SelectedItem is not TaskDefinition selected)
        {
            MessageBox.Show(this, "请先选择一条任务。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var editor = new TaskEditorWindow(selected.Clone(), GetTaskSnapshot(), _holidayDates) { Owner = this };
        TaskDefinition? result = null;
        if (!ShowDialogWithSchedulerPaused(editor, out result) || result is null)
        {
            return;
        }

        selected.UpdateFrom(result);
        NormalizeOrder();
        RecomputeHealthIssues(logIssues: true);
        UpdateTaskRuntimeAndNextTrigger(DateTimeOffset.Now, currentTaskId: null);
        _taskView.Refresh();
        UpdateDashboardCards();
        UpdateSelectedTaskDetail();

        UpdateActionButtonsState();
        await PersistSafelyAsync("编辑任务");
        _scheduler.Refresh();
        SelectTaskById(selected.Id);
    }

    private async void DeleteTaskButton_Click(object sender, RoutedEventArgs e)
    {
        if (TaskGrid.SelectedItem is not TaskDefinition selected)
        {
            MessageBox.Show(this, "请先选择一条任务。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirm = MessageBox.Show(this, $"确定删除任务“{selected.Name}”？", "确认", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        _taskRows.Remove(selected);
        TaskGrid.SelectedItem = null;
        NormalizeOrder();
        RecomputeHealthIssues(logIssues: false);
        UpdateTaskRuntimeAndNextTrigger(DateTimeOffset.Now, currentTaskId: null);
        _taskView.Refresh();
        UpdateDashboardCards();
        UpdateSelectedTaskDetail();

        UpdateActionButtonsState();
        await PersistSafelyAsync("删除任务");
        _scheduler.Refresh();
    }

    private async void ToggleTaskButton_Click(object sender, RoutedEventArgs e)
    {
        if (TaskGrid.SelectedItem is not TaskDefinition selected)
        {
            MessageBox.Show(this, "请先选择一条任务。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        selected.IsEnabled = !selected.IsEnabled;
        selected.RuntimeStatus = selected.IsEnabled ? "等待中" : "已停用";

        RecomputeHealthIssues(logIssues: false);
        UpdateTaskRuntimeAndNextTrigger(DateTimeOffset.Now, currentTaskId: null);
        _taskView.Refresh();
        UpdateDashboardCards();
        UpdateSelectedTaskDetail();

        UpdateActionButtonsState();
        await PersistSafelyAsync("启用停用任务");
        _scheduler.Refresh();
    }

    private async void TestPlaybackButton_Click(object sender, RoutedEventArgs e)
    {
        if (TaskGrid.SelectedItem is not TaskDefinition selected)
        {
            MessageBox.Show(this, "请先选择一条任务。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            await _scheduler.PlayPreviewAsync(selected);
        }
        catch (Exception ex)
        {
            _errorCenter.Report("测试播放", $"任务 {selected.Name} 播放失败", ex.ToString());
            MessageBox.Show(this, $"测试播放失败：{ex.Message}", "播放异常", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void StopPlaybackButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _scheduler.StopPlaybackAsync();
        }
        catch (Exception ex)
        {
            _errorCenter.Report("停止播放", "停止播放失败", ex.ToString());
            MessageBox.Show(this, $"停止播放失败：{ex.Message}", "播放异常", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void ManageHolidayButton_Click(object sender, RoutedEventArgs e)
    {
        var window = new HolidaySettingsWindow(_holidayDates) { Owner = this };
        if (!ShowDialogWithSchedulerPaused(window))
        {
            return;
        }

        _holidayDates = window.ResultDates.Distinct().OrderBy(d => d).ToList();

        UpdateTaskRuntimeAndNextTrigger(DateTimeOffset.Now, currentTaskId: null);
        UpdateSelectedTaskDetail();
        _taskView.Refresh();

        UpdateActionButtonsState();
        await PersistSafelyAsync("节假日设置");
        _scheduler.Refresh();
    }

    private void OpenErrorCenterButton_Click(object sender, RoutedEventArgs e)
    {
        var window = new ErrorCenterWindow(_errorCenter) { Owner = this };
        window.ShowDialog();
    }

    private void MinimizeToTrayButton_Click(object sender, RoutedEventArgs e) => HideToTray();

    private void CloseButton_Click(object sender, RoutedEventArgs e) => HideToTray();

    private void QuickFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || _suppressUiEvents)
        {
            return;
        }

        _taskView.Refresh();
        UpdateDashboardCards();
        UpdateActionButtonsState();
        ScheduleUiPersist();
    }

    private void ColumnToggleChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded || _suppressUiEvents)
        {
            return;
        }

        ApplyColumnToggles();
        UpdateActionButtonsState();
        ScheduleUiPersist();
    }

    private void TaskGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateSelectedTaskDetail();
        UpdateActionButtonsState();
    }

    private void RefreshPreviewButton_Click(object sender, RoutedEventArgs e)
    {
        UpdateSelectedTaskPreview();
    }

    private void TaskGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (!IsDoubleClickOnRow(e.OriginalSource as DependencyObject))
        {
            return;
        }

        EditTaskButton_Click(sender, e);
    }

    private void HideToTray()
    {
        Hide();
        ShowInTaskbar = false;
        _notifyIcon.BalloonTipTitle = "星晨音频助手";
        _notifyIcon.BalloonTipText = "程序仍在后台运行，可从托盘恢复。";
        _notifyIcon.ShowBalloonTip(1200);
    }

    private void RestoreFromTray()
    {
        Show();
        ShowInTaskbar = true;
        WindowState = WindowState.Normal;
        Activate();
    }

    private ContextMenuStrip BuildTrayMenu()
    {
        var menu = new ContextMenuStrip();

        var openItem = new ToolStripMenuItem("打开主界面");
        openItem.Click += (_, _) => Dispatcher.Invoke(RestoreFromTray);

        var stopItem = new ToolStripMenuItem("停止播放");
        stopItem.Click += (_, _) => Dispatcher.Invoke(() => _ = _scheduler.StopPlaybackAsync());

        var errorItem = new ToolStripMenuItem("错误中心");
        errorItem.Click += (_, _) => Dispatcher.Invoke(() =>
        {
            var window = new ErrorCenterWindow(_errorCenter) { Owner = this };
            window.ShowDialog();
        });

        var exitItem = new ToolStripMenuItem("退出");
        exitItem.Click += (_, _) => Dispatcher.Invoke(() =>
        {
            _allowClose = true;
            Close();
        });

        menu.Items.Add(openItem);
        menu.Items.Add(stopItem);
        menu.Items.Add(errorItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);
        return menu;
    }

    private IReadOnlyList<TaskDefinition> GetTaskSnapshot()
    {
        return _taskRows
            .OrderBy(task => task.SortOrder)
            .Select(task =>
            {
                var clone = task.Clone();
                if (clone.HasHealthIssue)
                {
                    clone.IsEnabled = false;
                }

                return clone;
            })
            .ToList();
    }

    private void OnSchedulerSnapshotUpdated(SchedulerSnapshot snapshot)
    {
        Dispatcher.Invoke(() =>
        {
            CurrentPlaybackText.Text = snapshot.CurrentTrackName;
            NextTriggerText.Text = snapshot.NextTrigger;
            SchedulerStatusText.Text = snapshot.SchedulerStatus;
            ConflictHintText.Text = snapshot.ConflictHint;
            _currentPlayingTaskId = snapshot.CurrentTaskId;

            if (!string.IsNullOrWhiteSpace(snapshot.LastError) && !string.Equals(_lastSchedulerError, snapshot.LastError, StringComparison.Ordinal))
            {
                _lastSchedulerError = snapshot.LastError;
                _errorCenter.Report("调度器", "调度异常", snapshot.LastError);
            }

            UpdateTaskRuntimeAndNextTrigger(snapshot.SnapshotTime, snapshot.CurrentTaskId);
            RefreshFilteredViewIfNeeded(snapshot.CurrentTaskId);
            UpdateDashboardCards();
            UpdateSelectedTaskDetail(refreshPreview: false);
            UpdateActionButtonsState();
        });
    }

    private void UpdateTaskRuntimeAndNextTrigger(DateTimeOffset now, Guid? currentTaskId)
    {
        var today = DateOnly.FromDateTime(now.LocalDateTime.Date);

        foreach (var task in _taskRows)
        {
            if (!task.IsEnabled)
            {
                task.RuntimeStatus = "已停用";
                task.NextTriggerText = "--";
                continue;
            }

            if (currentTaskId.HasValue && task.Id == currentTaskId.Value)
            {
                task.RuntimeStatus = "播放中";
            }
            else if (task.PauseUntilDate.HasValue && today <= task.PauseUntilDate.Value)
            {
                task.RuntimeStatus = "暂停中";
            }
            else if (task.HasHealthIssue)
            {
                task.RuntimeStatus = "异常";
            }
            else
            {
                task.RuntimeStatus = "等待中";
            }

            var next = ScheduleCalculator.GetNextStart(task.ToScheduleRule(), now, IsHolidayDate);
            task.NextTriggerText = next?.ToString("MM-dd ddd HH:mm") ?? "--";

            if (!currentTaskId.HasValue &&
                task.RecurrenceMode == TaskRecurrenceMode.OneTime &&
                !task.HasHealthIssue &&
                next is null)
            {
                task.RuntimeStatus = "已完成";
            }
        }
    }

    private void UpdateDashboardCards()
    {
        EnabledCountText.Text = _taskRows.Count(task => task.IsEnabled).ToString();
        PlayingCountText.Text = _taskRows.Count(task => task.RuntimeStatus == "播放中").ToString();
        IssueCountText.Text = _taskRows.Count(task => task.HasHealthIssue).ToString();
    }

    private void UpdateSelectedTaskDetail(bool refreshPreview = true)
    {
        if (TaskGrid.SelectedItem is not TaskDefinition selected)
        {
            SelectedTaskNameText.Text = "未选择任务";
            SelectedTaskTimeText.Text = "时间段：--";
            SelectedTaskStrategyText.Text = "策略：--";
            SelectedTaskAudioText.Text = "文件：--";
            SelectedTaskPriorityText.Text = "优先级：--";
            SelectedTaskFadeText.Text = "淡入/淡出：--";
            SelectedTaskRuntimeText.Text = "状态：--";
            SelectedTaskHealthText.Text = "健康：--";
            SelectedTaskPreviewList.ItemsSource = null;
            return;
        }

        SelectedTaskNameText.Text = selected.Name;
        SelectedTaskTimeText.Text = $"时间段：{selected.TimeRange}";
        SelectedTaskStrategyText.Text = $"策略：{selected.StrategyText}";
        SelectedTaskAudioText.Text = $"文件：{selected.AudioPath}";
        SelectedTaskPriorityText.Text = $"优先级：{selected.Priority}";
        SelectedTaskFadeText.Text = $"淡入/淡出：{selected.FadeDisplay}";
        SelectedTaskRuntimeText.Text = $"状态：{selected.RuntimeStatus}";
        SelectedTaskHealthText.Text = string.IsNullOrWhiteSpace(selected.HealthIssue)
            ? "健康：正常"
            : $"健康：{selected.HealthIssue}";

        if (refreshPreview)
        {
            UpdateSelectedTaskPreview();
        }
    }

    private void UpdateSelectedTaskPreview()
    {
        if (TaskGrid.SelectedItem is not TaskDefinition selected)
        {
            SelectedTaskPreviewList.ItemsSource = null;
            return;
        }

        var preview = TaskConflictService.BuildPreview(selected, DateTimeOffset.Now, _holidayDates, 7)
            .Select(dt => $"{dt:yyyy-MM-dd ddd HH:mm}")
            .ToList();

        if (preview.Count == 0)
        {
            preview.Add("暂无后续触发（可能被策略过滤）");
        }

        SelectedTaskPreviewList.ItemsSource = preview;
    }

    private void RecomputeHealthIssues(bool logIssues)
    {
        foreach (var task in _taskRows)
        {
            var issue = TaskHealthService.GetHealthIssue(task) ?? string.Empty;
            var changed = !string.Equals(task.HealthIssue, issue, StringComparison.Ordinal);
            task.HealthIssue = issue;

            if (logIssues && changed && !string.IsNullOrWhiteSpace(issue))
            {
                _errorCenter.Report("健康检查", $"任务 {task.Name} 存在问题", issue);
            }
        }
    }

    private async Task PersistCoreAsync(CancellationToken cancellationToken = default)
    {
        CaptureUiState();
        var config = TaskDefinitionMapper.ToConfiguration(_taskRows, _holidayDates, _uiConfig);
        await _persistGate.WaitAsync(cancellationToken);
        try
        {
            await _configStore.SaveAsync(config, cancellationToken);
        }
        finally
        {
            _persistGate.Release();
        }
    }

    private async Task PersistSafelyAsync(string source, CancellationToken cancellationToken = default)
    {
        try
        {
            await PersistCoreAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // No-op.
        }
        catch (Exception ex)
        {
            _errorCenter.Report(source, "保存配置失败", ex.ToString());
        }
    }

    private void ScheduleUiPersist()
    {
        _uiPersistPending = true;
        _uiPersistDebounceTimer.Stop();
        _uiPersistDebounceTimer.Start();
    }

    private async void UiPersistDebounceTimer_Tick(object? sender, EventArgs e)
    {
        _uiPersistDebounceTimer.Stop();
        if (!_uiPersistPending)
        {
            return;
        }

        _uiPersistPending = false;
        await PersistSafelyAsync("界面状态更新");
    }

    private void CaptureUiState()
    {
        _uiConfig.QuickFilter = (QuickFilterComboBox.SelectedItem as FilterOption)?.Mode.ToString() ?? QuickFilterMode.All.ToString();
        _uiConfig.SortMode = "Manual";
        _uiConfig.Columns = GetColumnMappings()
            .Select(entry => new ColumnPreference
            {
                Key = entry.Key,
                IsVisible = entry.Column.Visibility == Visibility.Visible,
                Width = entry.Column.Width.IsAbsolute ? entry.Column.Width.DisplayValue : double.NaN
            })
            .ToList();
    }

    private void ApplyUiFromConfig()
    {
        _suppressUiEvents = true;
        try
        {
            var mode = ParseQuickFilter(_uiConfig.QuickFilter);
            var target = QuickFilterComboBox.Items.OfType<FilterOption>().FirstOrDefault(entry => entry.Mode == mode);
            if (target is not null)
            {
                QuickFilterComboBox.SelectedItem = target;
            }

            var map = _uiConfig.Columns.ToDictionary(c => c.Key, StringComparer.OrdinalIgnoreCase);
            foreach (var entry in GetColumnMappings())
            {
                if (!map.TryGetValue(entry.Key, out var pref))
                {
                    continue;
                }

                entry.Column.Visibility = pref.IsVisible ? Visibility.Visible : Visibility.Collapsed;
                if (!double.IsNaN(pref.Width) && pref.Width > 40)
                {
                    entry.Column.Width = new DataGridLength(pref.Width, DataGridLengthUnitType.Pixel);
                }
            }

            ToggleStrategyColumn.IsChecked = StrategyColumn.Visibility == Visibility.Visible;
            ToggleNextColumn.IsChecked = NextColumn.Visibility == Visibility.Visible;
            ToggleHealthColumn.IsChecked = HealthColumn.Visibility == Visibility.Visible;

            ApplyColumnToggles();
        }
        finally
        {
            _suppressUiEvents = false;
        }

        _taskView.Refresh();
        UpdateActionButtonsState();
    }

    private void ApplyColumnToggles()
    {
        StrategyColumn.Visibility = ToggleStrategyColumn.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        NextColumn.Visibility = ToggleNextColumn.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        HealthColumn.Visibility = ToggleHealthColumn.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private bool FilterTask(object obj)
    {
        if (obj is not TaskDefinition task)
        {
            return false;
        }

        var mode = (QuickFilterComboBox.SelectedItem as FilterOption)?.Mode ?? QuickFilterMode.All;
        return mode switch
        {
            QuickFilterMode.All => true,
            QuickFilterMode.Enabled => task.IsEnabled,
            QuickFilterMode.Playing => task.RuntimeStatus == "播放中",
            QuickFilterMode.Issue => task.HasHealthIssue,
            _ => true
        };
    }

    private bool IsHolidayDate(DateOnly date) => _holidayDates.Contains(date);

    private void RefreshFilteredViewIfNeeded(Guid? currentTaskId)
    {
        var mode = (QuickFilterComboBox.SelectedItem as FilterOption)?.Mode ?? QuickFilterMode.All;
        if (mode != QuickFilterMode.Playing)
        {
            return;
        }

        if (_lastFilterPlayingTaskId == currentTaskId)
        {
            return;
        }

        _lastFilterPlayingTaskId = currentTaskId;
        _taskView.Refresh();
    }

    private void ReplaceTasks(IEnumerable<TaskDefinition> tasks)
    {
        _taskRows.Clear();
        foreach (var task in tasks.OrderBy(task => task.SortOrder))
        {
            _taskRows.Add(task);
        }

        NormalizeOrder();
    }

    private void NormalizeOrder()
    {
        for (var i = 0; i < _taskRows.Count; i++)
        {
            _taskRows[i].SortOrder = i;
        }
    }

    private void SelectTaskById(Guid id)
    {
        var task = _taskRows.FirstOrDefault(row => row.Id == id);
        if (task is null)
        {
            UpdateActionButtonsState();
            return;
        }

        TaskGrid.SelectedItem = task;
        TaskGrid.ScrollIntoView(task);
        UpdateActionButtonsState();
    }

    private void UpdateActionButtonsState()
    {
        var hasSelection = TaskGrid.SelectedItem is TaskDefinition selected && _taskRows.Contains(selected);

        EditTaskButton.IsEnabled = hasSelection;
        DeleteTaskButton.IsEnabled = hasSelection;
        ToggleTaskButton.IsEnabled = hasSelection;
        TestPlaybackButton.IsEnabled = hasSelection;
        RefreshPreviewButton.IsEnabled = hasSelection;
        StopPlaybackButton.IsEnabled = _currentPlayingTaskId.HasValue;
    }

    private IEnumerable<(string Key, DataGridColumn Column)> GetColumnMappings()
    {
        yield return ("enabled", EnabledColumn);
        yield return ("name", NameColumn);
        yield return ("time", TimeColumn);
        yield return ("strategy", StrategyColumn);
        yield return ("audio", AudioColumn);
        yield return ("priority", PriorityColumn);
        yield return ("status", StatusColumn);
        yield return ("next", NextColumn);
        yield return ("health", HealthColumn);
    }

    private static QuickFilterMode ParseQuickFilter(string? raw)
    {
        if (Enum.TryParse<QuickFilterMode>(raw, ignoreCase: true, out var mode))
        {
            return mode;
        }

        return QuickFilterMode.All;
    }

    private static bool IsDoubleClickOnRow(DependencyObject? source)
    {
        var current = source;
        while (current is not null)
        {
            if (current is DataGridRow)
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    private bool ShowDialogWithSchedulerPaused(Window window)
    {
        _scheduler.Pause();
        try
        {
            return window.ShowDialog() == true;
        }
        finally
        {
            _scheduler.Resume();
        }
    }

    private bool ShowDialogWithSchedulerPaused(TaskEditorWindow window, out TaskDefinition? result)
    {
        _scheduler.Pause();
        try
        {
            var confirmed = window.ShowDialog() == true;
            result = confirmed ? window.Result : null;
            return confirmed;
        }
        finally
        {
            _scheduler.Resume();
        }
    }

    private sealed record FilterOption(QuickFilterMode Mode, string Label)
    {
        public override string ToString() => Label;
    }
}
