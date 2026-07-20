using System.Diagnostics;
using System.IO;
using NoiseReduction.Core.Logging;

namespace NoiseReduction.App.Services;

/// <summary>
/// Uses AudioDeviceCmdlets PowerShell module to switch system default audio devices.
/// The module DLL is bundled in the native\ directory and loaded via absolute path.
/// </summary>
public static class AudioDeviceSwitcher
{
  private static string? _adcModulePath;

  private static string GetModulePath()
  {
    if (_adcModulePath != null)
      return _adcModulePath;

    var path = Path.Combine(AppContext.BaseDirectory, "native", "AudioDeviceCmdlets.dll");
    if (!File.Exists(path))
      throw new FileNotFoundException("AudioDeviceCmdlets.dll not found", path);

    _adcModulePath = path;
    return path;
  }

  /// <summary>
  /// Set the system default audio device by device ID.
  /// Sets both the default device and the default communication device.
  /// Runs PowerShell silently in a hidden process — user is unaware.
  /// </summary>
  public static bool SetDefaultCaptureDevice(string deviceId)
  {
    try
    {
      if (string.IsNullOrWhiteSpace(deviceId))
      {
        AppLogger.Instance.Warn($"SetDefaultCaptureDevice called with empty deviceId. Caller: {new System.Diagnostics.StackTrace()}");
        return false;
      }
      var modulePath = GetModulePath();
      // Set-AudioDevice -ID sets both default and default-comm for the device type
      var command = $"Import-Module '{modulePath}'; Set-AudioDevice -ID '{deviceId}'";

      AppLogger.Instance.Debug($"Switching default capture device: {deviceId}");

      var psi = new ProcessStartInfo("powershell.exe")
      {
        Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{command}\"",
        WindowStyle = ProcessWindowStyle.Hidden,
        CreateNoWindow = true,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true
      };

      using var process = Process.Start(psi);
      if (process == null)
      {
        AppLogger.Instance.Warn("Failed to start PowerShell for device switch");
        return false;
      }

      process.WaitForExit(5000);

      if (process.ExitCode != 0)
      {
        var error = process.StandardError.ReadToEnd().Trim();
        AppLogger.Instance.Warn($"Device switch failed (code {process.ExitCode}): {error}");
        return false;
      }

      AppLogger.Instance.Debug($"Default capture device switched to: {deviceId}");
      return true;
    }
    catch (Exception ex)
    {
      AppLogger.Instance.Error(ex, "Failed to switch default capture device");
      return false;
    }
  }
}
