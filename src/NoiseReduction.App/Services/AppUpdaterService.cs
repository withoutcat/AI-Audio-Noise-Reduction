using System.Net.Http;
using System.IO;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using NoiseReduction.Core.Logging;

namespace NoiseReduction.App.Services;

public class AppUpdaterService
{
  private readonly string _repoApi = "https://api.github.com/repos/withoutcat/AI-Audio-Noise-Reduction/releases/latest";
  private readonly string _currentVersion;

  public AppUpdaterService(string currentVersion)
  {
    _currentVersion = currentVersion;
  }

  public async Task<UpdateInfo?> CheckForUpdateAsync()
  {
    AppLogger.Instance.Debug($"检查更新: currentVersion={_currentVersion}, api={_repoApi}");
    using var client = new HttpClient();
    client.DefaultRequestHeaders.UserAgent.ParseAdd($"ANR/{_currentVersion}");
    client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
    client.Timeout = TimeSpan.FromSeconds(10);

    string json;
    try
    {
      json = await client.GetStringAsync(_repoApi);
    }
    catch (HttpRequestException ex)
    {
      AppLogger.Instance.Info($"无法连接 GitHub: {ex.Message}");
      return null;
    }
    catch (TaskCanceledException)
    {
      AppLogger.Instance.Info("无法连接 GitHub (请求超时)");
      return null;
    }

    AppLogger.Instance.Debug($"更新响应已接收, 长度={json.Length}");
    using var doc = JsonDocument.Parse(json);
    var root = doc.RootElement;

    var tag = root.GetProperty("tag_name").GetString()?.TrimStart('v');
    AppLogger.Instance.Debug($"解析到 tag_name={tag}");
    if (tag == null)
    {
      AppLogger.Instance.Debug("tag_name 为空，无法获取版本号");
      return null;
    }

    if (!Version.TryParse(tag, out var latestVersion))
    {
      AppLogger.Instance.Debug($"无法解析最新版本号: {tag}");
      return null;
    }

    var current = Version.Parse(_currentVersion);
    if (latestVersion < current)
    {
      AppLogger.Instance.Debug($"当前版本已是最新: current={current}, latest={latestVersion}");
      return null;
    }

    // Parse assets
    var assets = root.GetProperty("assets");
    string? downloadUrl = null;
    string? sha256 = null;
    foreach (var asset in assets.EnumerateArray())
    {
      var name = asset.GetProperty("name").GetString();
      if (name != null && name.EndsWith("-win-x64.exe"))
      {
        downloadUrl = asset.GetProperty("browser_download_url").GetString();

        // Parse digest: "sha256:4df28e0fe988c7f36ba48d6e6116c2634b2d280374f49936f1db112ebceeeffe"
        if (asset.TryGetProperty("digest", out var digestEl))
        {
          var digest = digestEl.GetString();
          if (digest != null && digest.StartsWith("sha256:"))
            sha256 = digest["sha256:".Length..];
        }
        break;
      }
    }

    if (downloadUrl == null)
    {
      AppLogger.Instance.Debug("未找到符合条件的 Windows 安装包资产");
      return null;
    }

    // Parse release notes (body)
    string? releaseNotes = null;
    if (root.TryGetProperty("body", out var bodyEl))
      releaseNotes = bodyEl.GetString();

    // Check if local cache already has a valid installer
    var localPath = FindLocalInstaller(downloadUrl, sha256, tag, _currentVersion);
    if (localPath != null)
      AppLogger.Instance.Debug($"发现本地安装包: {localPath}");

    AppLogger.Instance.Debug($"更新可用: latest={tag}, sha256={sha256}");
    return new UpdateInfo(tag, downloadUrl, releaseNotes, sha256, localPath);
  }

  /// <summary>Get the expected temp path for a given download URL.</summary>
  public static string GetExpectedTempPath(string downloadUrl)
  {
    var tempDir = Path.Combine(Path.GetTempPath(), "ANR-update");
    var fileName = Path.GetFileName(new Uri(downloadUrl).LocalPath);
    return Path.Combine(tempDir, fileName);
  }

  /// <summary>Compute SHA256 hash of a file.</summary>
  public static string ComputeSha256(string filePath)
  {
    using var stream = File.OpenRead(filePath);
    using var sha = SHA256.Create();
    var hash = sha.ComputeHash(stream);
    return Convert.ToHexStringLower(hash);
  }

  /// <summary>Check if a local installer file is valid (matches expected version + SHA256).</summary>
  public static bool IsLocalInstallerValid(string filePath, string? expectedSha256, string expectedVersion, string currentVersion)
  {
    if (!File.Exists(filePath)) return false;

    try
    {
      // Check ProductName + ProductVersion from file metadata
      var fvi = FileVersionInfo.GetVersionInfo(filePath);
      AppLogger.Instance.Debug($"检查本地安装包信息: {fvi}");
      AppLogger.Instance.Debug($"  ProductName={fvi.ProductName}, ProductVersion={fvi.ProductVersion}");
      if (fvi.ProductName?.Trim() != "AI Noise Reduction")
      {
        AppLogger.Instance.Debug($"  文件 ProductName 不匹配: {fvi.ProductName}");
        return false;
      }

      var productVersion = fvi.ProductVersion;
      if (productVersion == null || !Version.TryParse(productVersion, out var parsedVersion))
      {
        AppLogger.Instance.Debug($"  文件 ProductVersion 无效: {productVersion}");
        return false;
      }

      if (parsedVersion <= Version.Parse(currentVersion))
      {
        AppLogger.Instance.Debug($"  文件版本 ({productVersion}) 不大于当前版本 ({currentVersion})");
        return false;
      }

      // Verify SHA256 if expectedSha256 is available
      if (expectedSha256 != null)
      {
        var actualHash = ComputeSha256(filePath);
        if (!string.Equals(actualHash, expectedSha256, StringComparison.OrdinalIgnoreCase))
        {
          AppLogger.Instance.Debug($"  SHA256 不匹配: 期望={expectedSha256}, 实际={actualHash}");
          return false;
        }
      }

      AppLogger.Instance.Debug($"  本地缓存验证通过: {filePath}, version={productVersion}");
      return true;
    }
    catch (Exception ex)
    {
      AppLogger.Instance.Debug($"  验证本地文件失败: {ex.Message}");
      return false;
    }
  }

  /// <summary>Find a valid local installer in the temp directory.</summary>
  public static string? FindLocalInstaller(string downloadUrl, string? expectedSha256, string expectedVersion, string currentVersion)
  {
    var tempDir = Path.Combine(Path.GetTempPath(), "ANR-update");
    if (!Directory.Exists(tempDir)) return null;

    foreach (var exeFile in Directory.EnumerateFiles(tempDir, "*.exe", SearchOption.AllDirectories))
    {
      AppLogger.Instance.Debug($"检查本地缓存: {exeFile}");
      if (IsLocalInstallerValid(exeFile, expectedSha256, expectedVersion, currentVersion))
        return exeFile;
    }

    return null;
  }

  /// <summary>Check if any exe in the temp dir has matching product info but different SHA256 (tampered).</summary>
  public static bool CheckForTamperedInstaller(string downloadUrl, string expectedSha256, string expectedVersion)
  {
    var tempDir = Path.Combine(Path.GetTempPath(), "ANR-update");
    if (!Directory.Exists(tempDir)) return false;

    if (!Version.TryParse(expectedVersion, out var expVer)) return false;

    foreach (var exeFile in Directory.EnumerateFiles(tempDir, "*.exe", SearchOption.AllDirectories))
    {
      try
      {
        var fvi = FileVersionInfo.GetVersionInfo(exeFile);
        if (fvi.ProductName?.Trim() != "AI Noise Reduction") continue;

        var productVersion = fvi.ProductVersion?.Trim();
        if (string.IsNullOrEmpty(productVersion)) continue;
        if (!Version.TryParse(productVersion, out var fileVer)) continue;

        // File version must be >= expected (handles "1.2.0.0" vs "1.2.0" correctly)
        if (fileVer < expVer) continue;

        AppLogger.Instance.Debug($"检查文件SHA256: {exeFile}, productVersion={productVersion}");

        // Same product + version, check SHA256
        var localSha256 = ComputeSha256(exeFile);
        AppLogger.Instance.Debug($"  localSHA256={localSha256}, expectedSHA256={expectedSha256}");
        if (!string.Equals(localSha256, expectedSha256, StringComparison.OrdinalIgnoreCase))
        {
          AppLogger.Instance.Debug($"发现篡改文件: {exeFile}, localSha256={localSha256}, expected={expectedSha256}");
          return true;
        }
      }
      catch { }
    }
    return false;
  }

  public async Task<string> DownloadUpdateAsync(string downloadUrl, IProgress<int>? progress = null)
  {
    AppLogger.Instance.Debug($"开始下载更新, url={downloadUrl}");
    var tempDir = Path.Combine(Path.GetTempPath(), "ANR-update");
    Directory.CreateDirectory(tempDir);
    var fileName = Path.GetFileName(new Uri(downloadUrl).LocalPath);
    var filePath = Path.Combine(tempDir, fileName);
    AppLogger.Instance.Debug($"下载文件将保存到: {filePath}");

    using var client = new HttpClient();
    client.Timeout = TimeSpan.FromMinutes(10);

    using var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
    response.EnsureSuccessStatusCode();

    var totalBytes = response.Content.Headers.ContentLength ?? -1;
    await using var contentStream = await response.Content.ReadAsStreamAsync();
    await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

    var buffer = new byte[8192];
    long totalRead = 0;
    int lastReportedPct = -1;
    int bytesRead;
    while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
    {
      await fileStream.WriteAsync(buffer, 0, bytesRead);
      totalRead += bytesRead;

      if (totalBytes > 0 && progress != null)
      {
        int pct = (int)(totalRead * 100 / totalBytes);
        if (pct != lastReportedPct)
        {
          progress.Report(pct);
          lastReportedPct = pct;
        }
      }
    }

    AppLogger.Instance.Debug($"下载完成, 字节数={totalRead}");
    return filePath;
  }

  public static void InstallUpdate(string installerPath)
  {
    AppLogger.Instance.Debug($"启动安装更新, 路径={installerPath}");

    // Start installer process first (it runs independently)
    Process.Start(new ProcessStartInfo
    {
      FileName = installerPath,
      Arguments = "/SUPPRESSMSGBOXES /NORESTART",
      UseShellExecute = true
    });
    AppLogger.Instance.Info("安装程序已启动，正在优雅关闭当前应用...");

    // Trigger graceful shutdown via window close.
    // OnWindowClosing detects InstallerLaunched == true,
    // calls ForceStop (stop mic, switch back) → Shutdown
    if (System.Windows.Application.Current is App app)
    {
      app.InstallerLaunched = true;
      // Close MainWindow → OnWindowClosing (IsExiting=false, InstallerLaunched=true)
      // → ForceStop → ExitApplication
      System.Windows.Application.Current.MainWindow.Close();
    }
  }
}

public record UpdateInfo(string Version, string DownloadUrl, string? ReleaseNotes, string? Sha256, string? LocalPath);
