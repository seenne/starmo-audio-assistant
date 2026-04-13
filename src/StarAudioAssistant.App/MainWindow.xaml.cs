using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Media;
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
    private readonly SchedulerOrchestrator _scheduler;
    private readonly NotifyIcon _notifyIcon;
    private bool _allowClose;

    public MainWindow()
    {
        InitializeComponent();

        TaskGrid.ItemsSource = _taskRows;
        _scheduler = new SchedulerOrchestrator(GetTaskSnapshot, new NaudioPlaybackService());
        _scheduler.SnapshotUpdated += OnSchedulerSnapshotUpdated;

        _notifyIcon = new NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Information,
            Visible = true,
            Text = "星辰音频助手"
        };

        _notifyIcon.DoubleClick += (_, _) => RestoreFromTray();
        _notifyIcon.ContextMenuStrip = BuildTrayMenu();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var config = await _configStore.LoadAsync();
        var loaded = TaskDefinitionMapper.ToTaskDefinitions(config);

        if (loaded.Count == 0)
        {
            loaded = BuildDefaultTasks();
        }

        ReplaceTasks(loaded);
        await PersistAsync();

        _scheduler.Start();
    }

    private async void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        await _scheduler.DisposeAsync();
    }

    private void MinimizeToTrayButton_Click(object sender, RoutedEventArgs e) => HideToTray();

    private void CloseButton_Click(object sender, RoutedEventArgs e) => HideToTray();

    private async void AddTaskButton_Click(object sender, RoutedEventArgs e)
    {
        var editor = new TaskEditorWindow(null) { Owner = this };
        if (editor.ShowDialog() != true || editor.Result is null)
        {
            return;
        }

        editor.Result.SortOrder = _taskRows.Count;
        editor.Result.RuntimeStatus = editor.Result.IsEnabled ? "等待中" : "已停用";
        _taskRows.Add(editor.Result);

        NormalizeOrder();
        await PersistAsync();
        _scheduler.Refresh();
        TaskGrid.SelectedItem = editor.Result;
    }

    private async void EditTaskButton_Click(object sender, RoutedEventArgs e)
    {
        if (TaskGrid.SelectedItem is not TaskDefinition selected)
        {
            MessageBox.Show(this, "请先选择一条任务。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var editor = new TaskEditorWindow(selected.Clone()) { Owner = this };
        if (editor.ShowDialog() != true || editor.Result is null)
        {
            return;
        }

        selected.UpdateFrom(editor.Result);
        selected.RuntimeStatus = selected.IsEnabled ? "等待中" : "已停用";

        NormalizeOrder();
        await PersistAsync();
        _scheduler.Refresh();
        TaskGrid.SelectedItem = selected;
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
        NormalizeOrder();
        await PersistAsync();
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

        await PersistAsync();
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
            MessageBox.Show(this, $"测试播放失败：{ex.Message}", "播放异常", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void StopPlaybackButton_Click(object sender, RoutedEventArgs e)
    {
        await _scheduler.StopPlaybackAsync();
    }

    private void TaskGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (!IsDoubleClickOnRow(e.OriginalSource as DependencyObject))
        {
            return;
        }

        EditTaskButton_Click(sender, e);
    }

    private void ViewConflictButton_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(this, ConflictHintText.Text, "冲突详情", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void HideToTray()
    {
        Hide();
        ShowInTaskbar = false;
        _notifyIcon.BalloonTipTitle = "星辰音频助手";
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

        var exitItem = new ToolStripMenuItem("退出");
        exitItem.Click += (_, _) => Dispatcher.Invoke(() =>
        {
            _allowClose = true;
            Close();
        });

        menu.Items.Add(openItem);
        menu.Items.Add(stopItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);
        return menu;
    }

    private IReadOnlyList<TaskDefinition> GetTaskSnapshot()
    {
        return _taskRows
            .OrderBy(task => task.SortOrder)
            .Select(task => task.Clone())
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

            foreach (var task in _taskRows)
            {
                if (!task.IsEnabled)
                {
                    task.RuntimeStatus = "已停用";
                    task.NextTriggerText = "--";
                    continue;
                }

                task.RuntimeStatus = snapshot.CurrentTaskId == task.Id ? "播放中" : "等待中";
                var next = ScheduleCalculator.GetNextStart(task.ToScheduleRule(), DateTimeOffset.Now);
                task.NextTriggerText = next?.ToString("ddd HH:mm") ?? "--";
            }
        });
    }

    private async Task PersistAsync()
    {
        var config = TaskDefinitionMapper.ToConfiguration(_taskRows);
        await _configStore.SaveAsync(config);
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

    private static List<TaskDefinition> BuildDefaultTasks() =>
    [
        new TaskDefinition
        {
            Name = "周一晨间",
            AudioPath = "D:/audio/morning_focus.mp3",
            StartDay = DayOfWeek.Monday,
            StartTime = new TimeOnly(6, 0),
            EndDay = DayOfWeek.Monday,
            EndTime = new TimeOnly(8, 0),
            Priority = 100,
            IsEnabled = true,
            FadeInMs = 1500,
            FadeOutMs = 1500,
            SortOrder = 0
        },
        new TaskDefinition
        {
            Name = "周二深夜",
            AudioPath = "D:/audio/night_guard.mp3",
            StartDay = DayOfWeek.Tuesday,
            StartTime = new TimeOnly(23, 0),
            EndDay = DayOfWeek.Wednesday,
            EndTime = new TimeOnly(5, 0),
            Priority = 100,
            IsEnabled = true,
            FadeInMs = 1500,
            FadeOutMs = 1500,
            SortOrder = 1
        }
    ];
}
