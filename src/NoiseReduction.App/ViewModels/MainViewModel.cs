using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;
using NAudio.CoreAudioApi;
using NoiseReduction.App.Services;
using NoiseReduction.Core.Devices;
using NoiseReduction.Core.Logging;
using NoiseReduction.Core.Pipeline;
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
    
        ToggleCommand = new RelayCommand(Toggle, CanToggle);
        ClearLogCommand = new RelayCommand(() =>
        {
            AppLogger.Instance.Clear();
            LogEntries.Clear();
            LogCleared?.Invoke();
        });
        _statsTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _statsTimer.Tick += OnStatsTimerTick;
        RefreshDevices();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<AudioDeviceInfo> CaptureDevices { get; } = new();
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
                    _config.LastCaptureDeviceName = value.Name;
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


    public string VirtualMicphoneName => _config.VirtualMicphoneName;

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
            .Cast<System.Windows.Window>()
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

    private void RefreshDevices()
    {
        try
        {
            ReplaceItems(CaptureDevices, _deviceManager.GetCaptureDevices());

            // Try to restore last selected device by name
            if (!string.IsNullOrEmpty(_config.LastCaptureDeviceName))
            {
                var saved = CaptureDevices.FirstOrDefault(d =>
                    d.Name.Equals(_config.LastCaptureDeviceName, StringComparison.OrdinalIgnoreCase));
                if (saved != null)
                {
                    _selectedCaptureDevice = saved;
                    OnPropertyChanged(nameof(SelectedCaptureDevice));
                }
            }

            SelectedCaptureDevice ??= CaptureDevices.FirstOrDefault();

            // Apply saved AINS mode
            OnPropertyChanged(nameof(AinsMode));

            // Apply saved debug mode
            OnPropertyChanged(nameof(DebugMode));

            // Check if virtual device (CABLE Output) is the system default recording device

            StatusMessage = $"发现 {CaptureDevices.Count} 个麦克风。";
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
            var cableOutput = CaptureDevices.FirstOrDefault(d =>
                d.Name.Contains("CABLE Output", StringComparison.OrdinalIgnoreCase));
            if (cableOutput == null)
            {
                StatusMessage = "未检测到 VB-CABLE 虚拟设备。请先安装 VB-CABLE Virtual Audio Device。";
                AppLogger.Instance.Error("未检测到 VB-CABLE 虚拟设备。请先安装 VB-CABLE Virtual Audio Device。");
                return;
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

            // Save original mic ID for later restoration
            _originalDefaultMicId = AudioDeviceUtility.GetDefaultCaptureDeviceId();

            // Auto-switch to CABLE Output if enabled
            if (AutoSwitchMic)
            {
                SwitchToCableOutput();
            }

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

    private void Stop()
    {
        _session?.Dispose();
        _session = null;
        _isActive = false;
        _statsTimer.Stop();
        _cpuUsage = 0;
        _memoryUsageMB = 0;
        _lastCpuCheck = default;

        // Restore original default microphone if auto-switch was enabled
        if (AutoSwitchMic && _originalDefaultMicId != null)
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
            var renderDevices = await Task.Run(() => _deviceManager.GetRenderDevices());
            var cableOutput = renderDevices.FirstOrDefault(d =>
                d.Name.Contains(_config.VirtualMicphoneName, StringComparison.OrdinalIgnoreCase));

            if (cableOutput == null)
            {
                AppLogger.Instance.Warn($"未找到虚拟麦克风: {_config.VirtualMicphoneName}");
                return;
            }

            // Save original mic if not already saved
            if (_originalDefaultMicId == null)
            {
                _originalDefaultMicId = AudioDeviceUtility.GetDefaultCaptureDeviceId();
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
        if (_originalDefaultMicId == null) return;

        try
        {
            await Task.Run(() => AudioDeviceSwitcher.SetDefaultCaptureDevice(_originalDefaultMicId));
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
        OnPropertyChanged(nameof(VirtualMicphoneName));
            OnPropertyChanged(nameof(AutoSwitchMic));
        OnPropertyChanged(nameof(StartButtonTooltip));
        ToggleCommand.RaiseCanExecuteChanged();
    }

    private void ReplaceItems<T>(ObservableCollection<T> collection, IEnumerable<T> values)
    {
        collection.Clear();
        foreach (var value in values)
        {
            collection.Add(value);
        }
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
}
