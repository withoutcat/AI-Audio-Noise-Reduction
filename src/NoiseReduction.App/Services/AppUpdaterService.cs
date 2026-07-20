using System.Net.Http;
using System.IO;
using System.Diagnostics;
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

        var response = await client.GetStringAsync(_repoApi);
        AppLogger.Instance.Debug($"更新响应已接收, 长度={response.Length}");
        using var doc = JsonDocument.Parse(response);
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
        if (latestVersion <= current)
        {
            AppLogger.Instance.Debug($"当前版本已是最新: current={current}, latest={latestVersion}");
            return null;
        }

        var assets = root.GetProperty("assets");
        string? downloadUrl = null;
        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString();
            if (name != null && name.EndsWith("-win-x64.exe"))
            {
                downloadUrl = asset.GetProperty("browser_download_url").GetString();
                break;
            }
        }

        if (downloadUrl == null)
        {
            AppLogger.Instance.Debug("未找到符合条件的 Windows 安装包资产");
            return null;
        }

        AppLogger.Instance.Debug($"更新可用: latest={tag}, downloadUrl={downloadUrl}");
        return new UpdateInfo(tag, downloadUrl);
    }

    /// <summary>Get the expected temp path for a given download URL.</summary>
    public static string GetExpectedTempPath(string downloadUrl)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ANR-update");
        var fileName = Path.GetFileName(new Uri(downloadUrl).LocalPath);
        return Path.Combine(tempDir, fileName);
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

        // Stream-based download for progress tracking
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

    public void InstallUpdate(string installerPath)
    {
        AppLogger.Instance.Debug($"启动安装更新, 路径={installerPath}");

        // Signal that installer is waiting -> OnWindowClosing does graceful shutdown
        if (System.Windows.Application.Current is App app)
            app.InstallerLaunched = true;

        Process.Start(new ProcessStartInfo
        {
            FileName = installerPath,
            Arguments = "/SUPPRESSMSGBOXES /NORESTART",
            UseShellExecute = true
        });

        AppLogger.Instance.Info("安装程序已启动，由安装程序接管关闭流程");
    }
}

public record UpdateInfo(string Version, string DownloadUrl);
