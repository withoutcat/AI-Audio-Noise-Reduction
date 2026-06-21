using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;

namespace NoiseReduction.App.Services;

/// <summary>
/// Utility class for setting the default audio device programmatically.
/// Uses the Windows IPolicyConfig COM interface (Vista+).
/// </summary>
public static class AudioDeviceUtility
{
    private const string PolicyConfigId = "870af99c-171d-4f9e-af0d-e63df40c2bc9";
    private const string PolicyConfigClientId = "f8679f50-850a-41cf-9c72-430f290290c8";

    /// <summary>
    /// Try to set the default recording (capture) device.
    /// Returns true if successful, false if the COM call fails or is not supported.
    /// </summary>
    public static bool TrySetDefaultCaptureDevice(string deviceId)
    {
        try
        {
            var comType = Type.GetTypeFromCLSID(new Guid(PolicyConfigClientId));
            if (comType == null) return false;
            var policyConfig = (IPolicyConfig?)Activator.CreateInstance(comType);
            if (policyConfig == null) return false;
            policyConfig.SetDefaultEndpoint(deviceId, ERole.eConsole);
            policyConfig.SetDefaultEndpoint(deviceId, ERole.eCommunications);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Get the ID of the current default recording device.
    /// </summary>
    public static string? GetDefaultCaptureDeviceId()
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Console);
            return device.ID;
        }
        catch
        {
            return null;
        }
    }
}

// ── COM interop types ──────────────────────────────────────────

[ComImport, Guid("f8679f50-850a-41cf-9c72-430f290290c8")]
internal class CPolicyConfigVistaClient { }

[ComImport, Guid("870af99c-171d-4f9e-af0d-e63df40c2bc9"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPolicyConfig
{
    void GetMixFormat(string pszDeviceName, IntPtr ppFormat);
    void GetDeviceFormat(string pszDeviceName, bool bDefault, IntPtr ppFormat);
    void ResetDeviceFormat(string pszDeviceName);
    void SetDeviceFormat(string pszDeviceName, IntPtr pEndpointFormat, IntPtr pDefaultFormat);
    void GetProcessingPeriod(string pszDeviceName, bool bDefault, IntPtr pmftDefaultPeriod, IntPtr pmftMinimumPeriod);
    void SetProcessingPeriod(string pszDeviceName, IntPtr pmftDefaultPeriod, IntPtr pmftMinimumPeriod);
    void GetShareMode(string pszDeviceName, IntPtr pMode);
    void SetShareMode(string pszDeviceName, IntPtr pMode);
    void GetPropertyValue(string pszDeviceName, int cbKey, ref Guid guidKey, IntPtr pv);
    void SetPropertyValue(string pszDeviceName, int cbKey, ref Guid guidKey, IntPtr pv);
    [PreserveSig]
    int SetDefaultEndpoint(string pszDeviceName, ERole role);
    void SetEndpointVisibility(string pszDeviceName, bool bVisible);
}

internal enum ERole
{
    eConsole = 0,
    eMultimedia = 1,
    eCommunications = 2
}
