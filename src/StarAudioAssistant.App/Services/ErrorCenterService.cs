using System.Collections.ObjectModel;
using System.Text;

namespace StarAudioAssistant.App.Services;

public sealed class ErrorCenterService
{
    private const int MaxEntries = 200;

    public ObservableCollection<ErrorEntry> Entries { get; } = [];

    public void Report(string source, string message, string? details = null)
    {
        var entry = new ErrorEntry(DateTimeOffset.Now, source, message, details ?? string.Empty);
        Entries.Insert(0, entry);

        while (Entries.Count > MaxEntries)
        {
            Entries.RemoveAt(Entries.Count - 1);
        }
    }

    public string BuildDiagnosticsText()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Starmo Audio Assistant Diagnostics");
        sb.AppendLine($"Generated: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
        sb.AppendLine();

        foreach (var entry in Entries)
        {
            sb.AppendLine($"[{entry.When:yyyy-MM-dd HH:mm:ss}] {entry.Source} - {entry.Message}");
            if (!string.IsNullOrWhiteSpace(entry.Details))
            {
                sb.AppendLine(entry.Details);
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }
}

public sealed record ErrorEntry(DateTimeOffset When, string Source, string Message, string Details);
