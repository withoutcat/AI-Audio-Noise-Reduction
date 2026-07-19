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
        using var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"ANR/{_currentVersion}");
        client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        client.Timeout = TimeSpan.FromSeconds(10);

        var response = await client.GetStringAsync(_repoApi);
        using var doc = JsonDocument.Parse(response);
        var root = doc.RootElement;

        var tag = root.GetProperty("tag_name").GetString()?.TrimStart('v');
        if (tag == null) return null;

        if (!Version.TryParse(tag, out var latestVersion)) return null;
        var current = Version.Parse(_currentVersion);
        if (latestVersion <= current) return null;

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

        if (downloadUrl == null) return null;

        return new UpdateInfo(tag, downloadUrl);
    }

    public async Task<string> DownloadUpdateAsync(string downloadUrl)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ANR-update");
        Directory.CreateDirectory(tempDir);
        var fileName = Path.GetFileName(new Uri(downloadUrl).LocalPath);
        var filePath = Path.Combine(tempDir, fileName);

        using var client = new HttpClient();
        client.Timeout = TimeSpan.FromMinutes(10);
        var bytes = await client.GetByteArrayAsync(downloadUrl);
        await File.WriteAllBytesAsync(filePath, bytes);

        return filePath;
    }

    public void InstallUpdate(string installerPath)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = installerPath,
            Arguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART",
            UseShellExecute = true
        });

        System.Windows.Application.Current.Shutdown();
    }
}

public record UpdateInfo(string Version, string DownloadUrl);

