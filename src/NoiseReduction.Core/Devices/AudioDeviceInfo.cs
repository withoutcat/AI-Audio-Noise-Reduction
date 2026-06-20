namespace NoiseReduction.Core.Devices;

public sealed record AudioDeviceInfo(string Id, string Name, AudioDeviceFlow Flow)
{
    public override string ToString() => Name;
}
