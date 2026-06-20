using NoiseReduction.Core.Audio;

namespace NoiseReduction.Core.Denoise;

public interface IDenoiseProcessor : IDisposable
{
    DenoiseMode Mode { get; }

    AudioFormatSpec Format { get; }

    AudioFrame Process(AudioFrame input);
}
