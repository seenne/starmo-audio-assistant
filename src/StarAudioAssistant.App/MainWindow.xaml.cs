using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace StarAudioAssistant.App;

public partial class MainWindow : Window
{
    public ObservableCollection<ScheduledTaskRow> TaskRows { get; } =
    [
        new(true, "周一晨间", "Mon 06:00 -> Mon 08:00", "每周循环", "morning_focus.mp3", "1.5s / 1.5s", 100, "2026-04-20 06:00", "播放中"),
        new(true, "周二深夜", "Tue 23:00 -> Wed 05:00", "跨天循环", "night_guard.mp3", "1.5s / 1.5s", 100, "2026-04-14 23:00", "等待中"),
        new(true, "午间提醒", "Mon-Fri 12:20 -> 12:35", "工作日", "lunch_chime.mp3", "1.0s / 1.0s", 80, "2026-04-13 12:20", "等待中"),
        new(false, "备用播报", "Sat 09:00 -> Sat 10:00", "每周循环", "backup_voice.mp3", "2.0s / 2.0s", 60, "2026-04-18 09:00", "已停用")
    ];

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
    }

    private void MinimizeToTrayButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void TaskGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (TaskGrid.SelectedItem is not ScheduledTaskRow selected)
        {
            return;
        }

        MessageBox.Show($"这里会打开任务编辑弹窗：{selected.Name}", "编辑任务", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}

public sealed record ScheduledTaskRow(
    bool IsEnabled,
    string Name,
    string TimeRange,
    string RuleType,
    string AudioFileName,
    string Fade,
    int Priority,
    string NextTrigger,
    string Status);
