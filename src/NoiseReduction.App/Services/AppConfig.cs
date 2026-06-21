using System.IO;
using System.Text.Json;

namespace NoiseReduction.App.Services;

public sealed class AppConfig
{
    private static readonly string ConfigPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "config.json");

    public string? AppId { get; set; }
    public string? LastCaptureDeviceName { get; set; }
    public int LastAinsMode { get; set; }
    public bool DebugMode { get; set; }
    /// <summary>Name of the virtual audio device installed by our installer (e.g. "CABLE Output").</summary>
    public string VirtualMicphoneName { get; set; } = "CABLE Output";

    public static AppConfig Load()
    {
        if (!File.Exists(ConfigPath))
            return new AppConfig();

        try
        {
            var json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
        }
        catch
        {
            return new AppConfig();
        }
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(ConfigPath, json);
        }
        catch
        {
            // Silently fail - config save is non-critical
        }
    }
}
