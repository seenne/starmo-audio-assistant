using System.Collections.ObjectModel;
using System.Windows;
using MessageBox = System.Windows.MessageBox;

namespace StarAudioAssistant.App;

public partial class HolidaySettingsWindow : Window
{
    private readonly ObservableCollection<DateOnly> _dates;

    public HolidaySettingsWindow(IEnumerable<DateOnly> currentDates)
    {
        InitializeComponent();

        _dates = new ObservableCollection<DateOnly>(currentDates.OrderBy(d => d));
        DateListBox.ItemsSource = _dates;
        if (_dates.Count > 0)
        {
            DateListBox.SelectedIndex = 0;
        }

        UpdateRemoveButtonState();
    }

    public IReadOnlyList<DateOnly> ResultDates { get; private set; } = [];

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        if (HolidayDatePicker.SelectedDate is null)
        {
            MessageBox.Show(this, "请先选择日期。", "节假日设置", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var date = DateOnly.FromDateTime(HolidayDatePicker.SelectedDate.Value.Date);
        if (_dates.Contains(date))
        {
            MessageBox.Show(this, "该日期已存在。", "节假日设置", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _dates.Add(date);
        SortDates();
        DateListBox.SelectedItem = date;
        UpdateRemoveButtonState();
    }

    private void RemoveButton_Click(object sender, RoutedEventArgs e)
    {
        if (DateListBox.SelectedItem is not DateOnly selected)
        {
            MessageBox.Show(this, "请先选择一条日期。", "节假日设置", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _dates.Remove(selected);
        UpdateRemoveButtonState();
    }

    private void DateListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        UpdateRemoveButtonState();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        ResultDates = _dates.OrderBy(d => d).ToList();
        DialogResult = true;
        Close();
    }

    private void SortDates()
    {
        var sorted = _dates.OrderBy(d => d).ToList();
        _dates.Clear();
        foreach (var item in sorted)
        {
            _dates.Add(item);
        }
    }

    private void UpdateRemoveButtonState()
    {
        RemoveHolidayButton.IsEnabled = DateListBox.SelectedItem is DateOnly;
    }
}
