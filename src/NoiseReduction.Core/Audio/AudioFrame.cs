namespace NoiseReduction.Core.Audio;

public sealed class AudioFrame
{
    public AudioFrame(byte[] buffer, int byteCount, AudioFormatSpec format)
    {
        Buffer = buffer;
        ByteCount = byteCount;
        Format = format;
    }

    public byte[] Buffer { get; }

    public int ByteCount { get; }

    public AudioFormatSpec Format { get; }
}
