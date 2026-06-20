using NAudio.CoreAudioApi;
using NAudio.Wave;
using NoiseReduction.Core.Audio;
using NoiseReduction.Core.Denoise;
using NoiseReduction.Core.Devices;
using NoiseReduction.Core.Pipeline;

namespace NoiseReduction.Infrastructure.Pipeline;

public sealed class WasapiPassthroughSession : IAudioPipelineSession
{
    private readonly AudioDeviceInfo _captureDevice;
    private readonly AudioDeviceInfo _renderDevice;
    private readonly IDenoiseProcessor _processor;
    private readonly object _sync = new();

    private WasapiCapture? _capture;
    private WasapiOut? _render;
    private BufferedWaveProvider? _buffer;
    private long _totalBytesCaptured;
    private long _totalFramesProcessed;

    public WasapiPassthroughSession(
        AudioDeviceInfo captureDevice,
        AudioDeviceInfo renderDevice,
        IDenoiseProcessor processor)
    {
        _captureDevice = captureDevice;
        _renderDevice = renderDevice;
        _processor = processor;
    }

    public bool IsRunning { get; private set; }

    public long TotalBytesCaptured => Interlocked.Read(ref _totalBytesCaptured);

    public long TotalFramesProcessed => Interlocked.Read(ref _totalFramesProcessed);

    public void Start()
    {
        lock (_sync)
        {
            if (IsRunning)
            {
                return;
            }

            var captureEndpoint = GetEndpoint(_captureDevice.Id, DataFlow.Capture);
            var renderEndpoint = GetEndpoint(_renderDevice.Id, DataFlow.Render);

            _capture = new WasapiCapture(captureEndpoint);
            Interlocked.Exchange(ref _totalBytesCaptured, 0);
            Interlocked.Exchange(ref _totalFramesProcessed, 0);

            _buffer = new BufferedWaveProvider(_capture.WaveFormat)
            {
                BufferDuration = TimeSpan.FromMilliseconds(250),
                DiscardOnBufferOverflow = true
            };

            _render = new WasapiOut(renderEndpoint, AudioClientShareMode.Shared, false, 80);
            _render.Init(_buffer);

            _capture.DataAvailable += OnDataAvailable;
            _render.Play();
            _capture.StartRecording();

            IsRunning = true;
        }
    }

    public void Stop()
    {
        lock (_sync)
        {
            if (!IsRunning && _capture is null && _render is null)
            {
                return;
            }

            if (_capture is not null)
            {
                _capture.DataAvailable -= OnDataAvailable;
                _capture.StopRecording();
                _capture.Dispose();
                _capture = null;
            }

            _render?.Stop();
            _render?.Dispose();
            _render = null;
            _buffer = null;

            IsRunning = false;
        }
    }

    public void Dispose()
    {
        Stop();
        _processor.Dispose();
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        var buffer = new byte[e.BytesRecorded];
        Buffer.BlockCopy(e.Buffer, 0, buffer, 0, e.BytesRecorded);

        var waveFormat = _capture?.WaveFormat;
        var format = waveFormat is null
            ? AudioFormatSpec.DefaultSpeech
            : new AudioFormatSpec(waveFormat.SampleRate, waveFormat.Channels, waveFormat.BitsPerSample);

        var output = _processor.Process(new AudioFrame(buffer, e.BytesRecorded, format));
        _buffer?.AddSamples(output.Buffer, 0, output.ByteCount);

        Interlocked.Add(ref _totalBytesCaptured, e.BytesRecorded);
        Interlocked.Increment(ref _totalFramesProcessed);
    }

    private static MMDevice GetEndpoint(string id, DataFlow dataFlow)
    {
        using var enumerator = new MMDeviceEnumerator();
        var device = enumerator.GetDevice(id);

        if (device.DataFlow != dataFlow)
        {
            throw new InvalidOperationException($"Audio device '{device.FriendlyName}' has unexpected flow '{device.DataFlow}'.");
        }

        return device;
    }
}
