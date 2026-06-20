namespace NoiseReduction.Core.Devices;

public interface IAudioDeviceManager
{
    IReadOnlyList<AudioDeviceInfo> GetCaptureDevices();

    IReadOnlyList<AudioDeviceInfo> GetRenderDevices();
}
