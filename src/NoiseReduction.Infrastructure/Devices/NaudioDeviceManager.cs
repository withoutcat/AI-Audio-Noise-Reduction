using NAudio.CoreAudioApi;
using NoiseReduction.Core.Devices;

namespace NoiseReduction.Infrastructure.Devices;

public sealed class NaudioDeviceManager : IAudioDeviceManager
{
    public IReadOnlyList<AudioDeviceInfo> GetCaptureDevices()
    {
        return Enumerate(DataFlow.Capture, AudioDeviceFlow.Capture);
    }

    public IReadOnlyList<AudioDeviceInfo> GetRenderDevices()
    {
        return Enumerate(DataFlow.Render, AudioDeviceFlow.Render);
    }

    private static IReadOnlyList<AudioDeviceInfo> Enumerate(DataFlow dataFlow, AudioDeviceFlow flow)
    {
        using var enumerator = new MMDeviceEnumerator();
        return enumerator
            .EnumerateAudioEndPoints(dataFlow, DeviceState.Active)
            .Select(device => new AudioDeviceInfo(device.ID, device.FriendlyName, flow))
            .OrderBy(device => device.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }
}
