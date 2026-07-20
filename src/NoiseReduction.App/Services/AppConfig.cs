using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NoiseReduction.App.Services;

/// <summary>
/// Application configuration persisted to %LOCALAPPDATA%\AINoiseReduction\config.json.
///
/// Save behavior: reads existing JSON, patches only the fields we own, and writes back
/// preserving any unknown keys. This avoids silently wiping settings added by future versions
/// or other tools.
/// </summary>
public sealed class AppConfig
{
  private static readonly string ConfigPath = Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
      "AINoiseReduction",
      "config.json");

  // ── Owned fields ───────────────────────────────────────────────────
  public string? AppId { get; set; }
  public string? LastUserMicphoneID { get; set; }
  public int LastAinsMode { get; set; }
  public bool DebugMode { get; set; }
  public bool AutoSwitchMic { get; set; }
  /// <summary>Name of the virtual audio device installed by our installer (e.g. "CABLE Output").</summary>
  public string? DefaultVirtualMicphoneID { get; set; }

  // ── Load ───────────────────────────────────────────────────────────

  public static AppConfig Load()
  {
    if (!File.Exists(ConfigPath))
      return new AppConfig();

    try
    {
      var json = File.ReadAllText(ConfigPath);
      var node = JsonNode.Parse(json);
      if (node is JsonObject obj)
      {
        return new AppConfig
        {
          AppId = (string?)obj["AppId"],
          LastUserMicphoneID = (string?)obj["LastUserMicphoneID"],
          LastAinsMode = (int?)obj["LastAinsMode"] ?? 0,
          DebugMode = (bool?)obj["DebugMode"] ?? false,
          AutoSwitchMic = (bool?)obj["AutoSwitchMic"] ?? false,
          DefaultVirtualMicphoneID = (string?)obj["DefaultVirtualMicphoneID"],
        };
      }
      return new AppConfig();
    }
    catch
    {
      return new AppConfig();
    }
  }

  // ── Save (merge) ───────────────────────────────────────────────────

  public void Save()
  {
    try
    {
      var dir = Path.GetDirectoryName(ConfigPath)!;
      if (!Directory.Exists(dir))
        Directory.CreateDirectory(dir);

      // Read existing JSON (if any) to preserve unknown fields
      JsonObject root;
      if (File.Exists(ConfigPath))
      {
        try
        {
          root = (JsonNode.Parse(File.ReadAllText(ConfigPath)) as JsonObject) ?? new JsonObject();
        }
        catch
        {
          root = new JsonObject();
        }
      }
      else
      {
        root = new JsonObject();
      }

      // Merge our fields
      root["AppId"] = AppId;
      root["LastUserMicphoneID"] = LastUserMicphoneID;
      root["LastAinsMode"] = LastAinsMode;
      root["DebugMode"] = DebugMode;
      root["AutoSwitchMic"] = AutoSwitchMic;
      root["DefaultVirtualMicphoneID"] = DefaultVirtualMicphoneID;

      var json = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
      File.WriteAllText(ConfigPath, json);
    }
    catch (Exception ex)
    {
      // Log to debug output and AppLogger if available
      System.Diagnostics.Debug.WriteLine($"AppConfig.Save failed: {ex.Message}");
      try
      {
        Core.Logging.AppLogger.Instance.Warn($"配置文件保存失败: {ex.Message}");
      }
      catch { }
    }
  }
}


