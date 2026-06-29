using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NoiseReduction.Core.Devices;
using NoiseReduction.Core.Logging;
using NoiseReduction.Core.Pipeline;

namespace NoiseReduction.Infrastructure.Pipeline;

/// <summary>
/// 声网AI降噪管线会话
/// SDK负责麦克风采集和AI降噪，我们只负责输出到虚拟麦克风
/// </summary>
public sealed class AgoraAinsPipelineSession : IAudioPipelineSession, IDisposable
{
    private const string ChannelName = "denoise-test";

    private readonly string _appId;
    private AudioDeviceInfo? _captureDevice;
    private readonly AudioDeviceInfo _renderDevice;
    private int _ainsMode;
    private readonly AppLogger _logger;

    private IntPtr _bridgeDll;
    private IntPtr _fpInit;
    private IntPtr _fpSetAINS;
    private IntPtr _fpJoin;
    private IntPtr _fpLeave;
    private IntPtr _fpRegisterObserver;
    private IntPtr _fpRegisterCallback;
    private IntPtr _fpRelease;
    private IntPtr _fpSetRecordingDeviceById;
    private IntPtr _fpFollowSystemDevice;
    private IntPtr _fpGetRecordingDevice;

    private InitDelegate? _init;
    private SetAINSDelegate? _setAINS;
    private JoinDelegate? _join;
    private LeaveDelegate? _leave;
    private RegisterObserverDelegate? _registerObserver;
    private RegisterCallbackDelegate? _registerCallback;
    private ReleaseDelegate? _release;
    private SetRecordingDeviceByIdDelegate? _setRecordingDeviceById;
    private FollowSystemRecordingDeviceDelegate? _followSystemDevice;
    private GetRecordingDeviceDelegate? _getRecordingDevice;

    private WasapiOut? _wasapiOut;
    private WaveOutEvent? _waveOutEvent;
    private BufferedWaveProvider? _bufferProvider;
    private WaveFormat? _outputWaveFormat;
    private GCHandle _callbackHandle;
    private AudioFrameCallback? _audioCallback;

    private long _totalFramesProcessed;
    private long _totalBytesProcessed;

    public bool IsRunning { get; private set; }
    public long TotalFramesProcessed => _totalFramesProcessed;
    public long TotalBytesCaptured => _totalBytesProcessed;

    public AgoraAinsPipelineSession(
        string appId,
        AudioDeviceInfo? captureDevice,
        AudioDeviceInfo renderDevice,
        int ainsMode,
        AppLogger logger)
    {
        _appId = appId;
        _captureDevice = captureDevice;
        _renderDevice = renderDevice;
        _ainsMode = ainsMode;
        _logger = logger;
    }

    public void Start()
    {
        if (IsRunning) return;

        _logger.Info("正在启动AI降噪...");
        _logger.Info("正在加载桥接库...");

        // Load bridge DLL
        var bridgeDllPath = Path.Combine(AppContext.BaseDirectory, "native", "Bridge.dll");
        if (!File.Exists(bridgeDllPath))
        {
            throw new FileNotFoundException("Bridge DLL not found", bridgeDllPath);
        }

        _bridgeDll = NativeLibrary.Load(bridgeDllPath);

        _fpInit = NativeLibrary.GetExport(_bridgeDll, "Bridge_Init");
        _fpSetAINS = NativeLibrary.GetExport(_bridgeDll, "Bridge_SetAINSMode");
        _fpJoin = NativeLibrary.GetExport(_bridgeDll, "Bridge_JoinChannel");
        _fpLeave = NativeLibrary.GetExport(_bridgeDll, "Bridge_LeaveChannel");
        _fpRegisterObserver = NativeLibrary.GetExport(_bridgeDll, "Bridge_RegisterAudioObserver");
        _fpRegisterCallback = NativeLibrary.GetExport(_bridgeDll, "Bridge_RegisterAudioCallback");
        _fpRelease = NativeLibrary.GetExport(_bridgeDll, "Bridge_Release");

        _init = Marshal.GetDelegateForFunctionPointer<InitDelegate>(_fpInit);
        _setAINS = Marshal.GetDelegateForFunctionPointer<SetAINSDelegate>(_fpSetAINS);
        _join = Marshal.GetDelegateForFunctionPointer<JoinDelegate>(_fpJoin);
        _leave = Marshal.GetDelegateForFunctionPointer<LeaveDelegate>(_fpLeave);
        _registerObserver = Marshal.GetDelegateForFunctionPointer<RegisterObserverDelegate>(_fpRegisterObserver);
        _registerCallback = Marshal.GetDelegateForFunctionPointer<RegisterCallbackDelegate>(_fpRegisterCallback);
        _release = Marshal.GetDelegateForFunctionPointer<ReleaseDelegate>(_fpRelease);

        // Load device-related function pointers
        _fpSetRecordingDeviceById = NativeLibrary.GetExport(_bridgeDll, "Bridge_SetRecordingDeviceById");
        _fpFollowSystemDevice = NativeLibrary.GetExport(_bridgeDll, "Bridge_FollowSystemRecordingDevice");
        _fpGetRecordingDevice = NativeLibrary.GetExport(_bridgeDll, "Bridge_GetRecordingDevice");
        _setRecordingDeviceById = Marshal.GetDelegateForFunctionPointer<SetRecordingDeviceByIdDelegate>(_fpSetRecordingDeviceById);
        _followSystemDevice = Marshal.GetDelegateForFunctionPointer<FollowSystemRecordingDeviceDelegate>(_fpFollowSystemDevice);
        _getRecordingDevice = Marshal.GetDelegateForFunctionPointer<GetRecordingDeviceDelegate>(_fpGetRecordingDevice);

        // Setup NAudio output to virtual mic (render device = CABLE Input, which is a "speaker")
        var enumerator = new MMDeviceEnumerator();
        var renderDevice = enumerator.GetDevice(_renderDevice.Id);
        var deviceFormat = renderDevice.AudioClient.MixFormat;
        _logger.Verbose($"写入设备: {renderDevice.FriendlyName} [{_renderDevice.Id}]");
        _logger.Verbose($"设备格式: {deviceFormat.SampleRate}Hz, {deviceFormat.Channels}ch");

        // Create a BufferedWaveProvider at the device's native output format (48kHz stereo)
        // SDK delivers 48kHz stereo PCM directly via callback
        _outputWaveFormat = new WaveFormat(deviceFormat.SampleRate, 16, deviceFormat.Channels);
        _bufferProvider = new BufferedWaveProvider(_outputWaveFormat)
        {
            BufferDuration = TimeSpan.FromMilliseconds(200),
            DiscardOnBufferOverflow = true
        };

        // Strategy: Try WASAPI first (modern, lower latency, event-driven)
        // Fallback to WaveOutEvent if WASAPI fails with the virtual device

        bool wasapiOk = false;
        try
        {
            _wasapiOut = new WasapiOut(renderDevice, AudioClientShareMode.Shared, false, 50);
            _wasapiOut.Init(_bufferProvider);
            wasapiOk = true;
            _logger.Info("音频输出: WASAPI 共享模式");
        }
        catch (Exception exWasapi)
        {
            _logger.Verbose($"WASAPI 初始化失败: {exWasapi.Message}, 尝试 WaveOut");
            _wasapiOut?.Dispose();
            _wasapiOut = null;
        }

        if (!wasapiOk)
        {
            // Fallback: WaveOut (MME) — more compatible with older virtual devices
            int waveOutDeviceNum = -1;
            for (int i = 0; i < WaveOut.DeviceCount; i++)
            {
                var caps = WaveOut.GetCapabilities(i);
                _logger.Verbose($"WaveOut设备 #{i}: {caps.ProductName}");
                if (caps.ProductName.Contains("CABLE", StringComparison.OrdinalIgnoreCase))
                {
                    waveOutDeviceNum = i;
                    break;
                }
            }

            if (waveOutDeviceNum >= 0)
            {
                var waveOut = new WaveOutEvent();
                waveOut.DeviceNumber = waveOutDeviceNum;
                waveOut.Init(_bufferProvider);
                _waveOutEvent = waveOut;
                _logger.Info("音频输出: WaveOut");
            }
            else
            {
                throw new InvalidOperationException("无法初始化音频输出：WASAPI 和 WaveOut 均失败");
            }
        }

        // Register PCM callback
        _audioCallback = OnAudioFrame;
        _callbackHandle = GCHandle.Alloc(_audioCallback);
        _registerCallback(_audioCallback, IntPtr.Zero);

        // Initialize SDK
        _logger.Info("正在初始化声网引擎...");
        int ret = _init(_appId);
        if (ret != 0) throw new InvalidOperationException($"初始化失败: {ret}");

        // Register audio observer
        _logger.Info("正在注册音频观察者...");
        ret = _registerObserver();
        if (ret != 0) throw new InvalidOperationException($"注册音频观察者失败: {ret}");

        // Set recording device AND disable system default following
        if (_captureDevice != null)
        {
            var captureDeviceId = FindAgoraDeviceId(_captureDevice.Name);
            if (captureDeviceId != null)
            {
                ret = _setRecordingDeviceById(captureDeviceId);
                _logger.Verbose($"设置麦克风: {_captureDevice.Name} (返回: {ret})");
                _followSystemDevice(false);
                _logger.Verbose("已禁止跟随系统默认设备");
            }
        }

        // Join channel with our selected device
        _logger.Info("正在加入频道...");
        ret = _join(null, ChannelName, 0);
        if (ret != 0) throw new InvalidOperationException($"加入频道失败: {ret}");

        if (_captureDevice != null)
            _logger.Info($"当前降噪麦克风: {_captureDevice.Name}");

        // Enable AI降噪 (re-enabled now that raw audio capture works)
        var modeName = _ainsMode switch
        {
            0 => "均衡",
            1 => "强力",
            2 => "超低延迟",
            _ => "未知"
        };
        ret = _setAINS(1, _ainsMode);
        if (ret != 0) _logger.Verbose($"降噪设置返回: {ret}");

        // Start output
        _wasapiOut?.Play();
        _waveOutEvent?.Play();
        IsRunning = true;
        _logger.Info($"AI降噪已开启（{modeName}模式）");
    }

    public void Stop()
    {
        if (!IsRunning) return;

        _wasapiOut?.Stop();
        _wasapiOut?.Dispose();
        _wasapiOut = null;
        _waveOutEvent?.Stop();
        _waveOutEvent?.Dispose();
        _waveOutEvent = null;
        _leave?.Invoke();
        _release?.Invoke();

        if (_callbackHandle.IsAllocated)
        {
            _callbackHandle.Free();
        }

        IsRunning = false;
        _logger.Info("AI降噪已停止");
    }

    public void Dispose()
    {
        Stop();
    }

    private string? FindAgoraDeviceId(string deviceName)
    {
        // Load Agora device enumeration functions (if not already loaded)
        var fpGetDeviceCount = NativeLibrary.GetExport(_bridgeDll, "Bridge_GetRecordingDeviceCount");
        var fpGetDeviceInfo = NativeLibrary.GetExport(_bridgeDll, "Bridge_GetRecordingDeviceInfo");
        var getDeviceCount = Marshal.GetDelegateForFunctionPointer<GetRecordingDeviceCountDelegate>(fpGetDeviceCount);
        var getDeviceInfo = Marshal.GetDelegateForFunctionPointer<GetRecordingDeviceInfoDelegate>(fpGetDeviceInfo);

        int count = getDeviceCount();
        if (count <= 0) return null;

        var nameBuf = new byte[512];
        var idBuf = new byte[512];
        for (int i = 0; i < count; i++)
        {
            Array.Clear(nameBuf, 0, nameBuf.Length);
            Array.Clear(idBuf, 0, idBuf.Length);
            int ret = getDeviceInfo(i, nameBuf, nameBuf.Length, idBuf, idBuf.Length);
            if (ret != 0) continue;

            string agorName = System.Text.Encoding.UTF8.GetString(nameBuf).TrimEnd('\0');
            string agorId = System.Text.Encoding.UTF8.GetString(idBuf).TrimEnd('\0');

            if (agorName.Equals(deviceName, StringComparison.OrdinalIgnoreCase)
                || agorName.Contains(deviceName, StringComparison.OrdinalIgnoreCase)
                || deviceName.Contains(agorName, StringComparison.OrdinalIgnoreCase))
            {
                return agorId;
            }
        }
        return null;
    }

    /// <summary>
    /// Change the AINS mode while the session is running.
    /// Can be called anytime after JoinChannel.
    /// </summary>
    public void SetAinsMode(int mode)
    {
        if (!IsRunning || _setAINS == null) return;

        _ainsMode = mode;
        var modeName = mode switch
        {
            0 => "均衡",
            1 => "强力",
            2 => "超低延迟",
            _ => "未知"
        };
        int ret = _setAINS(1, mode);
        _logger.Info($"降噪等级已切换: {modeName}");
        if (ret != 0) _logger.Verbose($"降噪等级切换返回: {ret}");
    }

    /// <summary>
    /// Change the capture (microphone) device while the session is running.
    /// </summary>
    public void ChangeCaptureDevice(AudioDeviceInfo device)
    {
        if (!IsRunning || _setRecordingDeviceById == null || _followSystemDevice == null) return;

        var deviceId = FindAgoraDeviceId(device.Name);
        if (deviceId == null)
        {
            _logger.Info($"切换麦克风失败: 未找到设备 '{device.Name}' 的 Agora ID");
            return;
        }

        int ret = _setRecordingDeviceById(deviceId);
        if (ret == 0)
        {
            _captureDevice = device;
            _followSystemDevice(false);
            _logger.Info($"麦克风已切换: {device.Name}");
        }
        else
        {
            _logger.Verbose($"切换麦克风失败: {device.Name} (返回: {ret})");
        }
    }

    private void OnAudioFrame(IntPtr buffer, int samplesPerChannel, int channels, int sampleRate, int bytesPerSample, IntPtr userData)
    {
        // SDK now delivers 48kHz stereo PCM directly — no conversion needed.
        int byteCount = samplesPerChannel * channels * bytesPerSample;
        var pcm = new byte[byteCount];
        Marshal.Copy(buffer, pcm, 0, byteCount);

        _bufferProvider?.AddSamples(pcm, 0, byteCount);
        _totalFramesProcessed++;
        _totalBytesProcessed += byteCount;
    }

    /// <summary>
    /// Verify an AppID by attempting to initialize the Agora engine.
    /// Returns (isValid, sdkVersion, errorMessage).
    /// </summary>
    public static (bool IsValid, string SdkVersion, string ErrorMessage) VerifyAppId(string appId)
    {
        var bridgeDllPath = Path.Combine(AppContext.BaseDirectory, "native", "Bridge.dll");
        if (!File.Exists(bridgeDllPath))
        {
            var msg = $"Bridge DLL not found: {bridgeDllPath}";
            AppLogger.Instance.Error(msg);
            return (false, "", msg);
        }

        IntPtr dll = IntPtr.Zero;
        try
        {
            dll = NativeLibrary.Load(bridgeDllPath);

            var fpInit = NativeLibrary.GetExport(dll, "Bridge_Init");
            var fpGetVersion = NativeLibrary.GetExport(dll, "Bridge_GetSdkVersion");
            var fpRelease = NativeLibrary.GetExport(dll, "Bridge_Release");

            var init = Marshal.GetDelegateForFunctionPointer<InitDelegate>(fpInit);
            var getVersion = Marshal.GetDelegateForFunctionPointer<GetSdkVersionDelegate>(fpGetVersion);
            var release = Marshal.GetDelegateForFunctionPointer<ReleaseDelegate>(fpRelease);

            int ret = init(appId);
            if (ret != 0)
            {
                var msg = $"引擎初始化失败 (错误码: {ret})";
                AppLogger.Instance.Error(msg);
                return (false, "", msg);
            }

            var versionPtr = getVersion();
            string sdkVersion = Marshal.PtrToStringAnsi(versionPtr) ?? "未知";

            release();

            return (true, sdkVersion, "");
        }
        catch (Exception ex)
        {
            AppLogger.Instance.Error(ex, "VerifyAppId 异常");
            return (false, "", ex.Message);
        }
        finally
        {
            if (dll != IntPtr.Zero)
                NativeLibrary.Free(dll);
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int InitDelegate([MarshalAs(UnmanagedType.LPStr)] string appId);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr GetSdkVersionDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int SetAINSDelegate(int enabled, int mode);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int JoinDelegate(
        [MarshalAs(UnmanagedType.LPStr)] string? token,
        [MarshalAs(UnmanagedType.LPStr)] string channel,
        uint uid);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int LeaveDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int RegisterObserverDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void RegisterCallbackDelegate(AudioFrameCallback callback, IntPtr userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void ReleaseDelegate();

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void AudioFrameCallback(
        IntPtr buffer,
        int samplesPerChannel,
        int channels,
        int sampleRate,
        int bytesPerSample,
        IntPtr userData);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetRecordingDeviceCountDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetRecordingDeviceInfoDelegate(int index, [MarshalAs(UnmanagedType.LPArray)] byte[] nameBuf, int nameBufSize, [MarshalAs(UnmanagedType.LPArray)] byte[] idBuf, int idBufSize);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int SetRecordingDeviceByIdDelegate([MarshalAs(UnmanagedType.LPStr)] string deviceId);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int FollowSystemRecordingDeviceDelegate(bool enable);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetRecordingDeviceDelegate([MarshalAs(UnmanagedType.LPArray)] byte[] deviceIdBuf, int bufSize);
}
