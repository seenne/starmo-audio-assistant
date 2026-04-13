using System.Globalization;
using System.IO;
using System.Windows;
using StarAudioAssistant.App.Models;

namespace StarAudioAssistant.App;

public partial class TaskEditorWindow : Window
{
    private readonly TaskDefinition? _source;

    public TaskEditorWindow(TaskDefinition? source)
    {
        InitializeComponent();
        _source = source;

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
            PriorityTextBox.Text = "100";
            FadeInTextBox.Text = "1500";
            FadeOutTextBox.Text = "1500";
            EnabledCheckBox.IsChecked = true;
            return;
        }

        NameTextBox.Text = source.Name;
        AudioPathTextBox.Text = source.AudioPath;
        StartDayComboBox.SelectedItem = source.StartDay;
        EndDayComboBox.SelectedItem = source.EndDay;
        StartTimeTextBox.Text = source.StartTime.ToString("HH:mm", CultureInfo.InvariantCulture);
        EndTimeTextBox.Text = source.EndTime.ToString("HH:mm", CultureInfo.InvariantCulture);
        PriorityTextBox.Text = source.Priority.ToString(CultureInfo.InvariantCulture);
        FadeInTextBox.Text = source.FadeInMs.ToString(CultureInfo.InvariantCulture);
        FadeOutTextBox.Text = source.FadeOutMs.ToString(CultureInfo.InvariantCulture);
        EnabledCheckBox.IsChecked = source.IsEnabled;
    }

    public TaskDefinition? Result { get; private set; }

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
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        ValidationTextBlock.Text = string.Empty;

        if (string.IsNullOrWhiteSpace(NameTextBox.Text))
        {
            ValidationTextBlock.Text = "任务名称不能为空。";
            return;
        }

        if (string.IsNullOrWhiteSpace(AudioPathTextBox.Text) || !File.Exists(AudioPathTextBox.Text))
        {
            ValidationTextBlock.Text = "请选择存在的音频文件。";
            return;
        }

        if (StartDayComboBox.SelectedItem is not DayOfWeek startDay || EndDayComboBox.SelectedItem is not DayOfWeek endDay)
        {
            ValidationTextBlock.Text = "请选择开始/结束星期。";
            return;
        }

        if (!TimeOnly.TryParseExact(StartTimeTextBox.Text, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var startTime))
        {
            ValidationTextBlock.Text = "开始时间格式应为 HH:mm。";
            return;
        }

        if (!TimeOnly.TryParseExact(EndTimeTextBox.Text, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var endTime))
        {
            ValidationTextBlock.Text = "结束时间格式应为 HH:mm。";
            return;
        }

        if (!int.TryParse(PriorityTextBox.Text, out var priority) || priority < 0)
        {
            ValidationTextBlock.Text = "优先级必须是大于等于 0 的整数。";
            return;
        }

        if (!int.TryParse(FadeInTextBox.Text, out var fadeInMs) || fadeInMs < 0)
        {
            ValidationTextBlock.Text = "淡入毫秒必须是大于等于 0 的整数。";
            return;
        }

        if (!int.TryParse(FadeOutTextBox.Text, out var fadeOutMs) || fadeOutMs < 0)
        {
            ValidationTextBlock.Text = "淡出毫秒必须是大于等于 0 的整数。";
            return;
        }

        Result = new TaskDefinition
        {
            Id = _source?.Id ?? Guid.NewGuid(),
            Name = NameTextBox.Text.Trim(),
            AudioPath = AudioPathTextBox.Text.Trim(),
            StartDay = startDay,
            StartTime = startTime,
            EndDay = endDay,
            EndTime = endTime,
            Priority = priority,
            IsEnabled = EnabledCheckBox.IsChecked == true,
            FadeInMs = fadeInMs,
            FadeOutMs = fadeOutMs,
            SortOrder = _source?.SortOrder ?? 0,
            RuntimeStatus = "等待中"
        };

        DialogResult = true;
        Close();
    }
}
