using NoiseReduction.Core.Audio;
using NoiseReduction.Core.Denoise;

namespace NoiseReduction.Infrastructure.Denoise;

public sealed class PassthroughDenoiseProcessor : IDenoiseProcessor
{
    public PassthroughDenoiseProcessor(AudioFormatSpec format)
    {
        Format = format;
    }

    public DenoiseMode Mode => DenoiseMode.Passthrough;

    public AudioFormatSpec Format { get; }

    public AudioFrame Process(AudioFrame input) => input;

    public void Dispose()
    {
    }
}
