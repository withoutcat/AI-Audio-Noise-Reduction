using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.IO;
using System.Windows.Threading;
using NAudio.CoreAudioApi;
using NoiseReduction.Core.Devices;
using NoiseReduction.Core.Logging;
using NoiseReduction.Core.Pipeline;
using NoiseReduction.App.Services;

using NoiseReduction.Infrastructure.Devices;
using NoiseReduction.Infrastructure.Pipeline;

namespace NoiseReduction.App.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
  private readonly IAudioDeviceManager _deviceManager = new NaudioDeviceManager();
  private readonly DispatcherTimer _statsTimer;
  private readonly AppConfig _config;
  private IAudioPipelineSession? _session;
  private AudioDeviceInfo? _selectedCaptureDevice;
  private int _ainsMode = 0;
  private bool _isActive;  // true when starting or running (controls button state)
  private bool _debugMode;
  private bool _isTopMost;
  private bool _autoSwitchMic;
  private string _statusMessage = "选择麦克风，然后点击开始。";
  private string _appId = "";
  private string? _originalDefaultMicId;
  private double _cpuUsage;
  private long _memoryUsageMB;
  private readonly AppUpdaterService _updater = null!;
  private bool _updateAvailable;
  private string? _updateVersion;
  private string? _updateDownloadUrl;
  private int _downloadProgress;
  private bool _isDownloading;
  private TimeSpan _lastCpuTime;
  private DateTime _lastCpuCheck;

  public MainViewModel()
  {
    AppLogger.Instance.EntryAdded += OnLogEntryAdded;

    // Load config
    _config = AppConfig.Load();
    _appId = _config.AppId ?? "";
    _debugMode = _config.DebugMode;
    _autoSwitchMic = _config.AutoSwitchMic;
    _ainsMode = _config.LastAinsMode;
    _updater = new AppUpdaterService(GetCurrentVersion());

    ToggleCommand = new RelayCommand(Toggle, CanToggle);
    ClearLogCommand = new RelayCommand(() =>
    {
      AppLogger.Instance.Clear();
      LogEntries.Clear();
      LogCleared?.Invoke();
    });
    DownloadUpdateCommand = new RelayCommand(OnDownloadUpdate);
    CheckForUpdatesCommand = new RelayCommand(OnCheckForUpdates);
    _statsTimer = new DispatcherTimer
    {
      Interval = TimeSpan.FromMilliseconds(500)
    };
    _statsTimer.Tick += OnStatsTimerTick;
    RefreshCaptureDevices();
    _ = CheckForUpdateAsync();
  }

  public event PropertyChangedEventHandler? PropertyChanged;

  public ObservableCollection<AudioDeviceInfo> SystemCaptureDevices { get; } = [];

  public ObservableCollection<AudioDeviceInfo> SelectableCaptureDevices { get; } = [];

  public ObservableCollection<LogEntry> LogEntries { get; } = new();

  public AudioDeviceInfo? SelectedCaptureDevice
  {
    get => _selectedCaptureDevice;
    set
    {
      if (SetField(ref _selectedCaptureDevice, value))
      {
        // Save device name to config
        if (value != null)
        {
          _config.LastUserMicphoneID = value.Id;
          _config.Save();
        }

        // Mid-session device switching
        if (IsRunning && _session is AgoraAinsPipelineSession session && value != null)
        {
          session.ChangeCaptureDevice(value);
        }
        OnPropertyChanged(nameof(StartButtonTooltip));
        ToggleCommand.RaiseCanExecuteChanged();
      }
    }
  }

  public int AinsMode
  {
    get => _ainsMode;
    set
    {
      if (SetField(ref _ainsMode, value))
      {
        // Save to config
        _config.LastAinsMode = value;
        _config.Save();

        // Mid-session AINS mode switching
        if (IsRunning && _session is AgoraAinsPipelineSession session)
        {
          session.SetAinsMode(value);
        }
        ToggleCommand.RaiseCanExecuteChanged();
      }
    }
  }

  public string StatusMessage
  {
    get => _statusMessage;
    private set => SetField(ref _statusMessage, value);
  }

  public string ToggleButtonText => _isActive ? "停止" : "开始";
  public bool UpdateAvailable
  {
    get => _updateAvailable;
    private set
    {
      if (SetField(ref _updateAvailable, value))
      {
        OnPropertyChanged(nameof(DownloadButtonVisible));
      }
    }
  }

  public int DownloadProgress
  {
    get => _downloadProgress;
    private set
    {
      if (SetField(ref _downloadProgress, value))
      {
        OnPropertyChanged(nameof(DownloadProgressWidth));
        OnPropertyChanged(nameof(DownloadButtonContent));
      }
    }
  }

  public bool IsDownloading
  {
    get => _isDownloading;
    private set
    {
      if (SetField(ref _isDownloading, value))
      {
        OnPropertyChanged(nameof(DownloadButtonContent));
        OnPropertyChanged(nameof(DownloadButtonVisible));
      }
    }
  }

  public double DownloadProgressWidth => _downloadProgress * 0.5;

  public string DownloadButtonContent => _isDownloading ? $"{_downloadProgress}%" : "⬇";

  public bool DownloadButtonVisible => _updateAvailable || _isDownloading;

  public RelayCommand? DownloadUpdateCommand { get; }
  public RelayCommand? CheckForUpdatesCommand { get; }

  public string RunStateText => _isActive ? "运行中" : "已停止";
  public bool IsRunning => _session?.IsRunning == true;
  public bool DebugMode
  {
    get => _debugMode;
    set
    {
      if (SetField(ref _debugMode, value))
      {
        _config.DebugMode = value;
        _config.Save();
        OnPropertyChanged(nameof(ConnectivityText));
      }
    }
  }

  public bool AutoSwitchMic
  {
    get => _autoSwitchMic;
    set
    {
      if (SetField(ref _autoSwitchMic, value))
      {
        _config.AutoSwitchMic = value;
        _config.Save();

        // Handle runtime toggling while denoising is active
        if (IsRunning)
        {
          if (value)
          {
            // User just enabled auto-switch while running → switch to CABLE Output
            SwitchToCableOutput();
          }
          else
          {
            // User just disabled auto-switch while running → restore original mic
            RestoreOriginalMic();
          }
        }
      }
    }
  }

  public string AppId
  {
    get => _appId;
    private set
    {
      if (SetField(ref _appId, value))
      {
        _config.AppId = string.IsNullOrEmpty(value) ? null : value;
        _config.Save();
        OnPropertyChanged(nameof(HasAppId));
        OnPropertyChanged(nameof(ConnectivityText));
        ToggleCommand.RaiseCanExecuteChanged();
      }
    }
  }

  public bool HasAppId => !string.IsNullOrEmpty(_appId);

  public string ConnectivityText
  {
    get
    {
      if (!HasAppId) return "AppID: 未设置";
      var masked = _appId.Length > 8
          ? _appId[..4] + "****" + _appId[^4..]
          : "****";
      return $"AppID: {masked}";
    }
  }



  public string StartButtonTooltip
  {
    get
    {
      if (_isActive) return "停止AI降噪服务，释放系统资源";
      if (!HasAppId) return "请先配置并验证 AppID";
      if (SelectedCaptureDevice is null) return "请先选择需降噪的麦克风";
      return "初始化并开启AI降噪服务";
    }
  }


  public string VersionText => "v" +
      (typeof(MainViewModel).Assembly.GetName().Version?.ToString(3) ?? "0.0.0");

  public string TopMostTooltip => _isTopMost ? "放我下来" : "把我举高高";
  public string TopMostIcon => _isTopMost ? "📌" : "📍";

  public string ResourceText
  {
    get
    {
      UpdateResourceUsage();
      return $"CPU: {_cpuUsage:F1}% | 内存: {_memoryUsageMB} MB";
    }
  }

  public RelayCommand ToggleCommand { get; }
  public RelayCommand ClearLogCommand { get; }
  public event Action? LogCleared;

  /// <summary>
  /// Opens the AppID verification dialog.
  /// Only updates the AppId if the user successfully verified a new AppID.
  /// </summary>
  public void OpenAppIdDialog()
  {
    var dialog = new Views.AppIdDialog(_appId);
    var owner = System.Windows.Application.Current?.Windows
        .Cast<Window>()
        .FirstOrDefault(w => w.IsVisible);
    dialog.Owner = owner;
    if (dialog.ShowDialog() == true && dialog.WasVerified)
    {
      if (dialog.VerifiedAppId == "")
      {
        // User clicked "解除" — stop session first, then clear AppID
        if (_isActive) Stop();
        AppId = "";
        AppLogger.Instance.Info("AppID 已解除");
      }
      else if (dialog.VerifiedAppId != _appId)
      {
        // User verified a new AppID — update and persist it
        AppId = dialog.VerifiedAppId;
        AppLogger.Instance.Info("AppID 已验证并更新");
      }
    }
    // If !WasVerified or dialog cancelled, keep the old AppId unchanged
  }

  public void Dispose()
  {
    _statsTimer.Stop();
    _session?.Dispose();
    AppLogger.Instance.EntryAdded -= OnLogEntryAdded;
  }

  public void RefreshCaptureDevices()
  {
    try
    {
      var allCaptureDevices = _deviceManager.GetCaptureDevices().ToList();

      // Populate full system list (including CABLE Output for internal detection)
      SystemCaptureDevices.Clear();
      allCaptureDevices.ForEach(d => SystemCaptureDevices.Add(d));

      SelectableCaptureDevices.Clear();
      _deviceManager.GetCaptureDevices()
          .Where(d => IsNotCableOutputDevice(d.Name))
          .ToList()
          .ForEach(d => SelectableCaptureDevices.Add(d));

      // Try to restore last selected device by name
      if (!string.IsNullOrEmpty(_config.LastUserMicphoneID))
      {
        var saved = SelectableCaptureDevices.FirstOrDefault(d =>
            d.Name.Equals(_config.LastUserMicphoneID, StringComparison.OrdinalIgnoreCase));
        if (saved != null)
        {
          _selectedCaptureDevice = saved;
          OnPropertyChanged(nameof(SelectedCaptureDevice));
        }
      }

      SelectedCaptureDevice ??= SelectableCaptureDevices.FirstOrDefault();

      // Apply saved AINS mode
      OnPropertyChanged(nameof(AinsMode));

      // Apply saved debug mode
      OnPropertyChanged(nameof(DebugMode));

      StatusMessage = $"发现 {SelectableCaptureDevices.Count} 个麦克风。";
    }
    catch (Exception ex)
    {
      StatusMessage = $"枚举音频设备失败: {ex.Message}";
    }
    finally
    {
      ToggleCommand.RaiseCanExecuteChanged();
    }
  }

  private static bool IsNotCableOutputDevice(string deviceName)
  {
    return !deviceName.Contains("CABLE Output", StringComparison.OrdinalIgnoreCase)
           && !deviceName.Contains("CABLE Out 16ch", StringComparison.OrdinalIgnoreCase);
  }


  private async void Toggle()
  {
    if (_isActive)
    {
      Stop();
      return;
    }

    await StartAsync();
  }

  private static string CaptureToRenderDeviceName(string captureName)
  {
    if (captureName.Contains("CABLE Output", StringComparison.OrdinalIgnoreCase))
      return "CABLE Input (VB-Audio Virtual Cable)";
    if (captureName.Contains("CABLE Out 16ch", StringComparison.OrdinalIgnoreCase))
      return "CABLE In 16ch (VB-Audio Virtual Cable)";

    var renderName = captureName
        .Replace("Output", "Input", StringComparison.OrdinalIgnoreCase)
        .Replace(" Out ", " In ", StringComparison.OrdinalIgnoreCase);

    return renderName != captureName ? renderName : captureName;
  }

  private async Task StartAsync()
  {
    if (SelectedCaptureDevice is null)
    {
      StatusMessage = "请选择需降噪的麦克风。";
      return;
    }

    // Check AppID
    if (!HasAppId)
    {
      StatusMessage = "请先配置声网 AppID。";
      AppLogger.Instance.Info("请先点击状态栏的 AppID，配置并验证声网 AppID");
      return;
    }

    try
    {

      // Save original mic ID BEFORE any async operation
      _originalDefaultMicId = AudioDeviceUtility.GetDefaultCaptureDeviceId();
      var cableOutput = SystemCaptureDevices.FirstOrDefault(d =>
          !string.IsNullOrEmpty(_config.DefaultVirtualMicphoneID) &&
          d.Id == _config.DefaultVirtualMicphoneID)
      ?? SystemCaptureDevices.FirstOrDefault(d =>
          d.Name.Contains("CABLE Output", StringComparison.OrdinalIgnoreCase));
      if (cableOutput == null)
      {
        StatusMessage = "未检测到 VB-CABLE 虚拟设备。请先安装 VB-CABLE Virtual Audio Device。";
        AppLogger.Instance.Error("未检测到 VB-CABLE 虚拟设备。请先安装 VB-CABLE Virtual Audio Device。");
        return;
      }

      // If found by name but not in config yet, save for future use
      if (string.IsNullOrEmpty(_config.DefaultVirtualMicphoneID))
      {
        _config.DefaultVirtualMicphoneID = cableOutput.Id;
        _config.Save();
      }

      var renderDeviceName = CaptureToRenderDeviceName(cableOutput.Name);
      AppLogger.Instance.Debug($"虚拟设备: {cableOutput.Name} → 写入设备: {renderDeviceName}");

      var renderDevices = _deviceManager.GetRenderDevices().ToList();
      AppLogger.Instance.Debug($"系统输出设备(渲染设备)共 {renderDevices.Count} 个:");
      foreach (var rd in renderDevices)
        AppLogger.Instance.Debug($"  - {rd.Name}");

      var renderDevice = renderDevices.FirstOrDefault(d =>
          d.Name.Equals(renderDeviceName, StringComparison.OrdinalIgnoreCase))
      ?? renderDevices.FirstOrDefault(d =>
          d.Name.Contains(renderDeviceName.Split('(')[0].Trim(), StringComparison.OrdinalIgnoreCase));

      if (renderDevice is null)
      {
        StatusMessage = $"未找到渲染设备: {renderDeviceName}。请检查虚拟麦克风设置。";
        AppLogger.Instance.Error($"未找到渲染设备 {renderDeviceName}");
        return;
      }

      AppLogger.Instance.Debug($"匹配到渲染设备: {renderDevice.Name}");

      // Auto-switch to CABLE Output if enabled
      if (AutoSwitchMic)
      {
        SwitchToCableOutput();
      }


      // Create session to route denoised audio through CABLE Input (render device)
      _session = new AgoraAinsPipelineSession(
          _appId,
          SelectedCaptureDevice!,
          renderDevice,
          AinsMode,
          AppLogger.Instance);

      _isActive = true;
      _statsTimer.Start();
      RaiseStateChanged();
      await Task.Run(() => _session.Start());

      StatusMessage = "AI降噪运行中";
    }
    catch (Exception ex)
    {
      _session?.Dispose();
      _session = null;
      _isActive = false;
      _statsTimer.Stop();

      // If init failed because AppID is invalid, warn but keep it — user can retry
      if (ex.Message.StartsWith("初始化失败", StringComparison.Ordinal) && HasAppId)
      {
        AppLogger.Instance.Error($"AppID 验证失败，请确认 AppID 是否正确: {ex.Message}");
        StatusMessage = "AppID 验证失败，请重新配置。";
      }
      else
      {
        StatusMessage = $"启动失败: {ex.Message}";
        AppLogger.Instance.Error($"启动失败: {ex.Message}");
      }

      RaiseStateChanged();
    }
  }

  public void Stop()
  {
    _session?.Dispose();
    _session = null;
    _isActive = false;
    _statsTimer.Stop();
    _cpuUsage = 0;
    _memoryUsageMB = 0;
    _lastCpuCheck = default;

    // Restore original default microphone if auto-switch was enabled
    if (AutoSwitchMic && !string.IsNullOrEmpty(_originalDefaultMicId))
    {
      RestoreOriginalMic();
      _originalDefaultMicId = null;
    }

    StatusMessage = "降噪已停止";
    RaiseStateChanged();
  }

  /// <summary>
  /// Switch system default capture device to CABLE Output using AudioDeviceCmdlets.
  /// Called when auto-switch is enabled and denoising starts or is toggled on.
  /// </summary>
  private async void SwitchToCableOutput()
  {
    try
    {
      var captureDevices = await Task.Run(() => _deviceManager.GetCaptureDevices());
      var cableOutput = captureDevices.FirstOrDefault(d =>
          !string.IsNullOrEmpty(_config.DefaultVirtualMicphoneID) &&
          d.Id == _config.DefaultVirtualMicphoneID)
      ?? captureDevices.FirstOrDefault(d =>
          d.Name.Contains("CABLE Output", StringComparison.OrdinalIgnoreCase));

      if (cableOutput == null)
      {
        AppLogger.Instance.Warn("未找到虚拟麦克风(CABLE Output)，请检查 VB-CABLE 是否已安装。");
        return;
      }


      // Use AudioDeviceSwitcher (AudioDeviceCmdlets) to switch
      await Task.Run(() => AudioDeviceSwitcher.SetDefaultCaptureDevice(cableOutput.Id));
    }
    catch (Exception ex)
    {
      AppLogger.Instance.Error(ex, "切换麦克风失败");
    }
  }

  /// <summary>
  /// Restore the original default capture device.
  /// Called when denoising stops or auto-switch is toggled off while running.
  /// </summary>
  private async void RestoreOriginalMic()
  {
    if (string.IsNullOrEmpty(_originalDefaultMicId)) return;

    try
    {
      var deviceId = _originalDefaultMicId;
      await Task.Run(() => AudioDeviceSwitcher.SetDefaultCaptureDevice(deviceId));
    }
    catch (Exception ex)
    {
      AppLogger.Instance.Error(ex, "恢复麦克风失败");
    }
  }


  private void OnStatsTimerTick(object? sender, EventArgs e)
  {
    OnPropertyChanged(nameof(ResourceText));
  }

  private void OnLogEntryAdded(object? sender, LogEntry entry)
  {
    bool showEntry = entry.Level >= LogLevel.Info
                     || (_debugMode && entry.Level == LogLevel.Debug);
    if (!showEntry) return;

    System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
    {
      LogEntries.Add(entry);

      // Keep max 200 entries in the UI
      while (LogEntries.Count > 200)
        LogEntries.RemoveAt(0);
    });
  }

  private void UpdateResourceUsage()
  {
    try
    {
      var process = Process.GetCurrentProcess();
      var now = DateTime.UtcNow;
      var cpuTime = process.TotalProcessorTime;

      if (_lastCpuCheck != default)
      {
        var elapsed = now - _lastCpuCheck;
        var cpuDelta = cpuTime - _lastCpuTime;
        _cpuUsage = cpuDelta.TotalMilliseconds / elapsed.TotalMilliseconds / Environment.ProcessorCount * 100;
      }

      _lastCpuTime = cpuTime;
      _lastCpuCheck = now;
      _memoryUsageMB = process.WorkingSet64 / (1024 * 1024);
    }
    catch
    {
    }
  }

  private bool CanToggle() => _isActive || (SelectedCaptureDevice is not null && HasAppId);

  public void ToggleTopMost()
  {
    _isTopMost = !_isTopMost;
    OnPropertyChanged(nameof(TopMostTooltip));
    OnPropertyChanged(nameof(TopMostIcon));
    OnPropertyChanged(nameof(IsTopMost));
  }

  public bool IsTopMost => _isTopMost;

  private void RaiseStateChanged()
  {
    OnPropertyChanged(nameof(IsRunning));
    OnPropertyChanged(nameof(ToggleButtonText));
    OnPropertyChanged(nameof(RunStateText));
    OnPropertyChanged(nameof(ResourceText));
    OnPropertyChanged(nameof(DebugMode));
    OnPropertyChanged(nameof(ConnectivityText));
    OnPropertyChanged(nameof(AutoSwitchMic));
    OnPropertyChanged(nameof(StartButtonTooltip));
    ToggleCommand.RaiseCanExecuteChanged();
  }

  private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
  {
    if (EqualityComparer<T>.Default.Equals(field, value))
      return false;

    field = value;
    OnPropertyChanged(propertyName);
    return true;
  }

  private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
  {
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
  }
  private static string GetCurrentVersion()
  {
    return typeof(MainViewModel).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
  }

  private async Task CheckForUpdateAsync()
  {
    if (_updater == null) return;
    try
    {
      var info = await _updater.CheckForUpdateAsync();
      if (info != null)
      {
        _updateVersion = info.Version;
        _updateDownloadUrl = info.DownloadUrl;
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
        {
          UpdateAvailable = true;
          AppLogger.Instance.Info($"发现新版本 v{info.Version}");
        });
      }
    }
    catch (Exception ex)
    {
      AppLogger.Instance.Debug($"检查更新失败: {ex.Message}");
    }
  }

  private async void OnDownloadUpdate()
  {
    if (_updater == null || string.IsNullOrEmpty(_updateDownloadUrl)) return;
    try
    {
      // Check if already downloaded in temp
      var cachedPath = AppUpdaterService.GetExpectedTempPath(_updateDownloadUrl);
      if (File.Exists(cachedPath))
      {
        AppLogger.Instance.Info("检测到已下载的安装包，跳过下载");
        _updater.InstallUpdate(cachedPath);
        return;
      }

      IsDownloading = true;
      DownloadProgress = 0;
      AppLogger.Instance.Info("正在下载更新...");

      var lastLoggedPct = -1;
      var progress = new Progress<int>(pct =>
      {
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
              {
                DownloadProgress = pct;
              });

        // Log every 10%
        if (pct >= lastLoggedPct + 10 || pct == 100)
        {
          AppLogger.Instance.Info($"下载进度: {pct}%");
        }
      });

      var path = await _updater.DownloadUpdateAsync(_updateDownloadUrl, progress);
      DownloadProgress = 100;
      AppLogger.Instance.Info($"更新下载完成: {System.IO.Path.GetFileName(path)}");
      _updater.InstallUpdate(path);
    }
    catch (Exception ex)
    {
      AppLogger.Instance.Error(ex, "下载更新失败");
    }
    finally
    {
      IsDownloading = false;
    }
  }

  private void OnCheckForUpdates()
  {
    _ = CheckForUpdateAsync();
  }
}