namespace NoiseReduction.Core.Pipeline;

public interface IAudioPipelineSession : IDisposable
{
    bool IsRunning { get; }

    long TotalBytesCaptured { get; }

    long TotalFramesProcessed { get; }

    void Start();

    void Stop();
}
