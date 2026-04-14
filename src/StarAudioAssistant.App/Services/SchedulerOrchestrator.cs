using System.Windows.Threading;
using StarAudioAssistant.App.Models;
using StarAudioAssistant.Audio.Playback;
using StarAudioAssistant.Core.Scheduling;

namespace StarAudioAssistant.App.Services;

public sealed class SchedulerOrchestrator : IAsyncDisposable
{
    private readonly Func<IReadOnlyList<TaskDefinition>> _taskProvider;
    private readonly IAudioPlaybackService _playbackService;
    private readonly Func<DateOnly, bool> _isHoliday;
    private readonly DispatcherTimer _timer;
    private DateTimeOffset _lastTick;
    private ActivePlayback? _activePlayback;
    private bool _isTicking;
    private bool _disposed;
    private string? _lastError;

    public SchedulerOrchestrator(
        Func<IReadOnlyList<TaskDefinition>> taskProvider,
        IAudioPlaybackService playbackService,
        Func<DateOnly, bool>? isHoliday = null)
    {
        _taskProvider = taskProvider;
        _playbackService = playbackService;
        _isHoliday = isHoliday ?? (_ => false);
        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += OnTick;
    }

    public event Action<SchedulerSnapshot>? SnapshotUpdated;

    public void Start()
    {
        ThrowIfDisposed();
        _lastTick = DateTimeOffset.Now;
        _lastError = null;
        _timer.Start();
        PublishSnapshot(_lastTick, GetOrderedEnabledTasks());
    }

    public void Pause() => _timer.Stop();

    public void Resume()
    {
        if (!_timer.IsEnabled)
        {
            _lastTick = DateTimeOffset.Now;
            _timer.Start();
        }
    }

    public async Task PlayPreviewAsync(TaskDefinition task, CancellationToken cancellationToken = default)
    {
        await _playbackService.PlayLoopAsync(
            task.AudioPath,
            TimeSpan.FromMilliseconds(task.FadeInMs),
            TimeSpan.FromMilliseconds(task.FadeOutMs),
            cancellationToken);

        var start = DateTimeOffset.Now;
        _activePlayback = new ActivePlayback(task.Id, task.Name, start, ScheduleCalculator.GetEndBoundary(task.ToScheduleRule(), start));
        _lastError = null;
        PublishSnapshot(DateTimeOffset.Now, GetOrderedEnabledTasks());
    }

    public async Task StopPlaybackAsync(CancellationToken cancellationToken = default)
    {
        await _playbackService.StopAsync(TimeSpan.FromMilliseconds(800), cancellationToken);
        _activePlayback = null;
        PublishSnapshot(DateTimeOffset.Now, GetOrderedEnabledTasks());
    }

    public void Refresh()
    {
        PublishSnapshot(DateTimeOffset.Now, GetOrderedEnabledTasks());
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _timer.Stop();
        _timer.Tick -= OnTick;
        await _playbackService.StopAsync(TimeSpan.FromMilliseconds(500));
        await _playbackService.DisposeAsync();
        _disposed = true;
    }

    private async void OnTick(object? sender, EventArgs e)
    {
        if (_isTicking)
        {
            return;
        }

        _isTicking = true;
        try
        {
            await ProcessTickAsync(DateTimeOffset.Now);
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            PublishSnapshot(DateTimeOffset.Now, GetOrderedEnabledTasks());
        }
        finally
        {
            _isTicking = false;
        }
    }

    private async Task ProcessTickAsync(DateTimeOffset now)
    {
        var enabledTasks = GetOrderedEnabledTasks();

        // Sleep/wake or long scheduling gaps should not trigger catch-up starts.
        if (now - _lastTick > TimeSpan.FromSeconds(8))
        {
            if (_activePlayback is not null && now >= _activePlayback.EndBoundary)
            {
                await _playbackService.StopAsync(TimeSpan.FromMilliseconds(800));
                _activePlayback = null;
            }

            _lastTick = now;
            PublishSnapshot(now, enabledTasks);
            return;
        }

        var startEvents = GetStartEvents(enabledTasks, _lastTick, now);
        if (startEvents.Count > 0)
        {
            var winner = startEvents
                .OrderByDescending(entry => entry.Task.Priority)
                .ThenBy(entry => entry.Task.SortOrder)
                .First();

            await _playbackService.PlayLoopAsync(
                winner.Task.AudioPath,
                TimeSpan.FromMilliseconds(winner.Task.FadeInMs),
                TimeSpan.FromMilliseconds(winner.Task.FadeOutMs));

            _activePlayback = new ActivePlayback(
                winner.Task.Id,
                winner.Task.Name,
                winner.StartBoundary,
                ScheduleCalculator.GetEndBoundary(winner.Task.ToScheduleRule(), winner.StartBoundary));
            _lastError = null;
        }
        else if (_activePlayback is not null)
        {
            var currentTask = enabledTasks.FirstOrDefault(task => task.Id == _activePlayback.TaskId);
            if (currentTask is null || now >= _activePlayback.EndBoundary)
            {
                await _playbackService.StopAsync(TimeSpan.FromMilliseconds(currentTask?.FadeOutMs ?? 800));
                _activePlayback = null;
            }
        }

        _lastTick = now;
        PublishSnapshot(now, enabledTasks);
    }

    private List<TaskDefinition> GetOrderedEnabledTasks()
    {
        return _taskProvider()
            .Where(task => task.IsEnabled)
            .OrderByDescending(task => task.Priority)
            .ThenBy(task => task.SortOrder)
            .Select(task => task.Clone())
            .ToList();
    }

    private List<StartEvent> GetStartEvents(IReadOnlyList<TaskDefinition> tasks, DateTimeOffset fromExclusive, DateTimeOffset toInclusive)
    {
        var results = new List<StartEvent>();

        foreach (var task in tasks)
        {
            var next = ScheduleCalculator.GetNextStart(task.ToScheduleRule(), fromExclusive.AddTicks(1), _isHoliday);
            if (next is not null && next <= toInclusive)
            {
                results.Add(new StartEvent(task, next.Value));
            }
        }

        return results;
    }

    private void PublishSnapshot(DateTimeOffset now, IReadOnlyList<TaskDefinition> enabledTasks)
    {
        var nextTrigger = enabledTasks
            .Select(task => new
            {
                Task = task,
                Next = ScheduleCalculator.GetNextStart(task.ToScheduleRule(), now, _isHoliday)
            })
            .Where(entry => entry.Next is not null)
            .OrderBy(entry => entry.Next)
            .ThenByDescending(entry => entry.Task.Priority)
            .ThenBy(entry => entry.Task.SortOrder)
            .FirstOrDefault();

        var nextTriggerText = nextTrigger is null
            ? "无"
            : $"{nextTrigger.Next:ddd HH:mm} {nextTrigger.Task.Name}";

        var conflictHint = BuildConflictHint(now, enabledTasks);
        var schedulerStatus = string.IsNullOrWhiteSpace(_lastError)
            ? (_timer.IsEnabled ? "运行中" : "已暂停")
            : $"异常：{_lastError}";

        SnapshotUpdated?.Invoke(new SchedulerSnapshot(
            CurrentTaskId: _activePlayback?.TaskId,
            CurrentTrackName: _activePlayback?.TaskName ?? "无",
            NextTrigger: nextTriggerText,
            SchedulerStatus: schedulerStatus,
            ConflictHint: conflictHint,
            LastError: _lastError,
            SnapshotTime: now));
    }

    private string BuildConflictHint(DateTimeOffset now, IReadOnlyList<TaskDefinition> enabledTasks)
    {
        if (_activePlayback is null)
        {
            return "当前暂无冲突。";
        }

        var nextPotential = enabledTasks
            .Where(task => task.Id != _activePlayback.TaskId)
            .Select(task => new
            {
                Task = task,
                Next = ScheduleCalculator.GetNextStart(task.ToScheduleRule(), now, _isHoliday)
            })
            .Where(entry => entry.Next is not null)
            .OrderBy(entry => entry.Next)
            .ThenByDescending(entry => entry.Task.Priority)
            .ThenBy(entry => entry.Task.SortOrder)
            .FirstOrDefault();

        if (nextPotential is null || nextPotential.Next > _activePlayback.EndBoundary)
        {
            return "当前暂无冲突。";
        }

        return $"{nextPotential.Next:ddd HH:mm} 时，\"{nextPotential.Task.Name}\" 将抢占当前任务。";
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private sealed record StartEvent(TaskDefinition Task, DateTimeOffset StartBoundary);

    private sealed record ActivePlayback(Guid TaskId, string TaskName, DateTimeOffset StartBoundary, DateTimeOffset EndBoundary);
}

public sealed record SchedulerSnapshot(
    Guid? CurrentTaskId,
    string CurrentTrackName,
    string NextTrigger,
    string SchedulerStatus,
    string ConflictHint,
    string? LastError,
    DateTimeOffset SnapshotTime);
