using System.Windows;
using StarAudioAssistant.App.Services;
using MessageBox = System.Windows.MessageBox;

namespace StarAudioAssistant.App;

public partial class ErrorCenterWindow : Window
{
    private readonly ErrorCenterService _errorCenter;

    public ErrorCenterWindow(ErrorCenterService errorCenter)
    {
        InitializeComponent();
        _errorCenter = errorCenter;
        ErrorGrid.ItemsSource = _errorCenter.Entries;
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        var text = _errorCenter.BuildDiagnosticsText();
        System.Windows.Clipboard.SetText(text);
        MessageBox.Show(this, "诊断文本已复制到剪贴板。", "错误中心", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
