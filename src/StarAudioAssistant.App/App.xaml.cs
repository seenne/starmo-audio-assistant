using System.IO;
using System.Threading.Tasks;

namespace StarAudioAssistant.App;

public partial class App : System.Windows.Application
{
    private string LogDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "StarAudioAssistant",
            "logs");

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        base.OnStartup(e);
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        DispatcherUnhandledException -= OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        WriteFatalLog("UI线程异常", e.Exception.ToString());
    }

    private void OnUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        var detail = e.ExceptionObject?.ToString() ?? "未知异常";
        WriteFatalLog("未处理异常", detail);
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        WriteFatalLog("后台任务未观察异常", e.Exception.ToString());
        e.SetObserved();
    }

    private void WriteFatalLog(string source, string detail)
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            var path = Path.Combine(LogDirectory, $"fatal-{DateTime.Now:yyyyMMdd}.log");
            var lines = new[]
            {
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {source}",
                detail,
                string.Empty
            };
            File.AppendAllLines(path, lines);
        }
        catch
        {
            // Ignore all logging failures to avoid recursive crashes.
        }
    }
}
