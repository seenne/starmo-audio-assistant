using NAudio.Wave;

namespace StarAudioAssistant.Audio.Playback;

internal sealed class LoopStream : WaveStream
{
    private readonly WaveStream _sourceStream;

    public LoopStream(WaveStream sourceStream)
    {
        _sourceStream = sourceStream;
    }

    public override WaveFormat WaveFormat => _sourceStream.WaveFormat;

    public override long Length => _sourceStream.Length;

    public override long Position
    {
        get => _sourceStream.Position;
        set => _sourceStream.Position = value;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var totalBytesRead = 0;

        while (totalBytesRead < count)
        {
            var bytesRead = _sourceStream.Read(buffer, offset + totalBytesRead, count - totalBytesRead);
            if (bytesRead == 0)
            {
                _sourceStream.Position = 0;
                continue;
            }

            totalBytesRead += bytesRead;
        }

        return totalBytesRead;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _sourceStream.Dispose();
        }

        base.Dispose(disposing);
    }
}
