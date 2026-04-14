namespace StarAudioAssistant.Audio.Playback;

public interface IAudioPlaybackService : IAsyncDisposable
{
    bool IsPlaying { get; }

    string? CurrentTrackPath { get; }

    Task PlayLoopAsync(string audioPath, TimeSpan fadeIn, TimeSpan fadeOut, CancellationToken cancellationToken = default);

    Task StopAsync(TimeSpan fadeOut, CancellationToken cancellationToken = default);
}
