namespace NoiseReduction.Core.Audio;

public sealed record AudioFormatSpec(int SampleRate, int Channels, int BitsPerSample)
{
    public static AudioFormatSpec DefaultSpeech { get; } = new(48_000, 1, 16);
}
