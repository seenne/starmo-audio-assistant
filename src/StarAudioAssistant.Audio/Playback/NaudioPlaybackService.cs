using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace StarAudioAssistant.Audio.Playback;

public sealed class NaudioPlaybackService : IAudioPlaybackService
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, float> _normalizationCache = new(StringComparer.OrdinalIgnoreCase);
    private TrackContext? _current;
    private bool _disposed;

    public bool IsPlaying => _current is not null;

    public string? CurrentTrackPath => _current?.TrackPath;

    public async Task PlayLoopAsync(string audioPath, TimeSpan fadeIn, TimeSpan fadeOut, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(audioPath);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            ThrowIfDisposed();

            if (_current is not null && string.Equals(_current.TrackPath, audioPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (_current is not null)
            {
                await FadeOutAndDisposeAsync(_current, fadeOut, cancellationToken);
                _current = null;
            }

            var targetVolume = GetNormalizedTargetVolume(audioPath);
            var next = CreateTrack(audioPath, targetVolume);
            next.VolumeChannel.Volume = 0f;
            next.Output.Play();
            await FadeVolumeAsync(next.VolumeChannel, 0f, next.TargetVolume, fadeIn, cancellationToken);
            _current = next;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task StopAsync(TimeSpan fadeOut, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_disposed || _current is null)
            {
                return;
            }

            await FadeOutAndDisposeAsync(_current, fadeOut, cancellationToken);
            _current = null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await _gate.WaitAsync();
        try
        {
            if (_current is not null)
            {
                _current.Dispose();
                _current = null;
            }

            _disposed = true;
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
        }
    }

    private float GetNormalizedTargetVolume(string audioPath)
    {
        if (_normalizationCache.TryGetValue(audioPath, out var cached))
        {
            return cached;
        }

        var estimated = EstimateTargetVolume(audioPath);
        _normalizationCache[audioPath] = estimated;
        return estimated;
    }

    private static float EstimateTargetVolume(string audioPath)
    {
        try
        {
            using var reader = new AudioFileReader(audioPath);
            var sampleProvider = reader.ToSampleProvider();

            var channels = Math.Max(1, sampleProvider.WaveFormat.Channels);
            var maxSamples = sampleProvider.WaveFormat.SampleRate * channels * 12;
            var buffer = new float[4096];

            double sumSquares = 0;
            var sampleCount = 0;

            while (sampleCount < maxSamples)
            {
                var request = Math.Min(buffer.Length, maxSamples - sampleCount);
                var read = sampleProvider.Read(buffer, 0, request);
                if (read <= 0)
                {
                    break;
                }

                for (var i = 0; i < read; i++)
                {
                    var sample = buffer[i];
                    sumSquares += sample * sample;
                }

                sampleCount += read;
            }

            if (sampleCount <= 0)
            {
                return 1f;
            }

            var rms = Math.Sqrt(sumSquares / sampleCount);
            if (rms < 0.0005)
            {
                return 1f;
            }

            const double targetRms = 0.12;
            var gain = targetRms / rms;
            return (float)Math.Clamp(gain, 0.55, 1.45);
        }
        catch
        {
            return 1f;
        }
    }

    private static TrackContext CreateTrack(string audioPath, float targetVolume)
    {
        if (!File.Exists(audioPath))
        {
            throw new FileNotFoundException("Audio file not found", audioPath);
        }

        var reader = new AudioFileReader(audioPath);
        var loop = new LoopStream(reader);
        var volume = new WaveChannel32(loop)
        {
            PadWithZeroes = false,
            Volume = 1f
        };

        var enumerator = new MMDeviceEnumerator();
        var endpoint = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        var output = new WasapiOut(endpoint, AudioClientShareMode.Shared, true, 100);
        output.Init(volume);

        return new TrackContext(audioPath, output, volume, endpoint, enumerator, targetVolume);
    }

    private static async Task FadeOutAndDisposeAsync(TrackContext context, TimeSpan fadeOut, CancellationToken cancellationToken)
    {
        await FadeVolumeAsync(context.VolumeChannel, context.VolumeChannel.Volume, 0f, fadeOut, cancellationToken);
        context.Dispose();
    }

    private static async Task FadeVolumeAsync(WaveChannel32 channel, float from, float to, TimeSpan duration, CancellationToken cancellationToken)
    {
        if (duration <= TimeSpan.Zero)
        {
            channel.Volume = to;
            return;
        }

        const int intervalMs = 40;
        var steps = Math.Max(1, (int)Math.Ceiling(duration.TotalMilliseconds / intervalMs));

        for (var i = 0; i <= steps; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var t = (float)i / steps;
            channel.Volume = from + ((to - from) * t);
            await Task.Delay(intervalMs, cancellationToken);
        }

        channel.Volume = to;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private sealed class TrackContext : IDisposable
    {
        public TrackContext(
            string trackPath,
            WasapiOut output,
            WaveChannel32 volumeChannel,
            MMDevice endpoint,
            MMDeviceEnumerator enumerator,
            float targetVolume)
        {
            TrackPath = trackPath;
            Output = output;
            VolumeChannel = volumeChannel;
            Endpoint = endpoint;
            Enumerator = enumerator;
            TargetVolume = targetVolume;
        }

        public string TrackPath { get; }

        public WasapiOut Output { get; }

        public WaveChannel32 VolumeChannel { get; }

        public MMDevice Endpoint { get; }

        public MMDeviceEnumerator Enumerator { get; }

        public float TargetVolume { get; }

        public void Dispose()
        {
            Output.Stop();
            Output.Dispose();
            VolumeChannel.Dispose();
            Endpoint.Dispose();
            Enumerator.Dispose();
        }
    }
}
