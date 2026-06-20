# AI Audio Noise Reduction — 工作交接文档

> **Workspace:** c:\Users\sunzi\OneDrive\projects\AI-audio-noise-reduction

***

## 一、项目结构

```
AI-audio-noise-reduction/
├── src/
│   ├── NoiseReduction.Bridge/            # C++ 桥接 DLL (.cpp → .dll)
│   │   ├── bridge.cpp                    # 所有导出函数，封装声网 SDK
│   │   ├── build.bat                     # 用 VS Build Tools 编译 Bridge DLL
│   │   └── bin/                          # 编译产物 (Bridge DLL + SDK DLL)
│   │
│   ├── NoiseReduction.Core/              # 核心接口和模型（纯 C#，无外部依赖）
│   │   ├── Audio/
│   │   │   ├── AudioFrame.cs             # PCM 音频帧数据结构
│   │   │   └── AudioFormatSpec.cs        # 音频格式规格 (16kHz, 16-bit, mono)
│   │   ├── Devices/
│   │   │   ├── AudioDeviceInfo.cs         # 设备信息 record (Id, Name, Flow)
│   │   │   └── IAudioDeviceManager.cs     # 设备管理器接口
│   │   ├── Logging/
│   │   │   └── AppLogger.cs              # 线程安全日志 (Info/Debug 级别)
│   │   ├── Pipeline/
│   │   │   └── IAudioPipelineSession.cs   # 管线会话接口
│   │   └── Denoise/
│   │       └── IDenoiseProcessor.cs       # (保留) 降噪处理器接口
│   │
│   ├── NoiseReduction.Infrastructure/    # 实现层（依赖 NAudio + Bridge DLL）
│   │   ├── Devices/
│   │   │   └── NaudioDeviceManager.cs    # 用 NAudio 枚举系统音频设备
│   │   ├── Pipeline/
│   │   │   ├── AgoraAinsPipelineSession.cs  # ★核心: 声网AI降噪管线
│   │   │   └── WasapiPassthroughSession.cs  # (保留) WASAPI 直通测试管线
│   │   └── Denoise/
│   │       └── PassthroughDenoiseProcessor.cs  # (保留) 透传处理器
│   │
│   ├── NoiseReduction.App/              # WPF 桌面应用 (.NET 10)
│   │   ├── App.xaml / App.xaml.cs        # 应用入口，定义 AccentBrush/SurfaceBrush
│   │   ├── MainWindow.xaml               # GUI 布局（3个下拉框 + 日志面板 + 状态栏）
│   │   ├── MainWindow.xaml.cs            # 窗口代码（日志自动滚动）
│   │   ├── ViewModels/
│   │   │   ├── MainViewModel.cs          # ★核心: MVVM ViewModel
│   │   │   └── RelayCommand.cs           # ICommand 简单实现
│   │   └── NoiseReduction.App.csproj     # 项目文件 (配置 Bridge DLL 自动复制)
│   │
│   └── NoiseReduction.SdkPoC/           # (保留) 早期终端测试项目，只有编译产物
│
├── res/sdk/                              # 声网 SDK
│   └── Shengwang_Native_SDK_for_Windows_FULL/
│       └── sdk/
│           ├── high_level_api/include/    # 高层 API 头文件
│           ├── low_level_api/include/     # 底层 API 头文件
│           └── x86_64/                   # x64 DLL + .lib 文件
```

## 二、完整执行流程

### 用户操作流

1. **启动应用** → `MainViewModel` 构造函数调用 `RefreshDevices()`
   - 用 `NaudioDeviceManager.GetCaptureDevices()` 枚举所有**输入设备（麦克风）**
   - 分别填充"需降噪设备"和"降噪后虚拟麦克风"两个下拉框
   - 默认选中 CABLE Output 作为虚拟麦克风
2. **用户选择** → 三个下拉框
   - **需降噪设备**：选择物理麦克风（如"麦克风阵列 (Realtek(R) Audio)"）
   - **降噪等级**：均衡(0) / 强力(1) / 超低延迟(2)
   - **降噪后虚拟麦克风**：选择 CABLE Output (VB-Audio Virtual Cable)
3. **点击"开始"** → `MainViewModel.Start()` 调用

### 核心执行链路 (`AgoraAinsPipelineSession.Start()`)

```
① 加载 Bridge DLL → 获取所有 C 函数指针
② NAudio 设置输出 → 用 WaveOutEvent 写入 CABLE Input（渲染设备）
③ 创建 48kHz stereo BufferedWaveProvider（500ms 缓冲）
④ 注册 PCM 回调（C# → Bridge → Agora）
⑤ Bridge_Init(appId) → 初始化声网引擎
   - createAgoraRtcEngine()
   - initialize(ctx) 其中 autoRegisterAgoraExtensions = true
   - queryInterface → 获取 mediaEngine + audioDeviceManager
⑥ Bridge_RegisterAudioObserver() → 注册音频帧观察者
⑦ Bridge_JoinChannel() → 首次加入频道（初始化音频模块 SSP）
⑧ Bridge_LeaveChannel() → 立即离开
⑨ Thread.Sleep(100) → 等待音频模块就绪
⑩ ★ FindAgoraDeviceId() → 用 Agora SDK 自己的枚举 API
   匹配设备名，获取 Agora 格式的设备 ID
⑪ Bridge_SetRecordingDeviceById(agoraId) → 设置我们选的麦克风
⑫ Bridge_FollowSystemRecordingDevice(false) → 禁止跟随系统默认
⑬ Bridge_JoinChannel() → 重新加入频道，用我们选的设备开始采集
⑭ Bridge_SetAINSMode(1, mode) → 启用 AI 降噪
⑮ waveOut.Play() → 开始输出到 CABLE Input
```

### 音频数据流

```
物理麦克风
  → Agora SDK 采集（16kHz mono, 16-bit）
    → AI 降噪（setAINSMode）
      → onRecordAudioFrame 回调（每 ~64ms 触发, 1024 samples/call）
        → C# OnAudioFrame() 手动转换 16kHz mono → 48kHz stereo
          → BufferedWaveProvider.AddSamples()
            → WaveOutEvent 读取 → CABLE Input（虚拟扬声器）
              → VB-CABLE 内部连线 → CABLE Output（虚拟麦克风）
                → Windows 录音机或其他应用读取
```

### 关键设计细节

- **格式转换**：SDK 输出 16kHz/16-bit/mono（2048 bytes/callback），手动重复采样 3x 并复制到双声道 → 48kHz/16-bit/stereo（12288 bytes/callback）
- **输出策略**：优先 WaveOutEvent（对 VB-CABLE 兼容性最好），回退 WasapiOut 独占/共享模式
- **设备名映射**：`MainViewModel.CaptureToRenderDeviceName()` 将 "CABLE Output" → "CABLE Input"
- **日志过滤**：`_logger.Debug()` 不在 GUI 显示，`_logger.Info()` 显示在日志面板

## 三、踩坑记录

### 1. RtcEngineContext 结构体布局 (offset 必须正确)

- `eventHandler` 在 offset 0，`appId` 在 offset 8
- `memset(&ctx, 0, sizeof(ctx))` 会将 `autoRegisterAgoraExtensions` 清零！
- **必须在 memset 后显式设置** **`ctx.autoRegisterAgoraExtensions = true`**

### 2. AI 降噪扩展加载

- 不自己调用 `loadExtensionProvider`/`registerExtension`，依赖 `autoRegisterAgoraExtensions = true` 自动加载
- 手动注册会返回 -3 (ERR\_NOT\_READY)，因为已经被自动加载了

### 3. setAINSMode 调用时机

- 必须在 `joinChannel` 之后调用才生效

### 4. setRecordingDevice 不生效的根因

- `setRecordingDeviceById` 返回 0（成功），但 SDK 内部会跟随系统默认设备
- **必须联合调用** `followSystemRecordingDevice(false)` 禁止跟随
- **还必须**先 `join → leave` 初始化音频模块（SSP），否则设置不生效

### 5. Windows MMDevice ID ≠ Agora 设备 ID

- NAudio 枚举的 `MMDevice.ID`（如 `{0.0.1.00000000}.{guid}`）和设备名不能直接传给 `setRecordingDevice`
- 必须用 Agora SDK 的 `enumerateRecordingDevices()` 获取 Agora 格式的设备 ID
- `FindAgoraDeviceId()` 通过设备名匹配（模糊匹配）

### 6. WASAPI 格式不匹配

- SDK 输出 16kHz mono，但 VB-CABLE 的 CABLE Input 是 48kHz stereo
- MediaFoundationResampler 可能出现问题，改用**手动 PCM 转换**（sample repetition）

### 7. WaveOutEvent 设备选择

- `WaveOut.DeviceNumber = -1` 会输出到系统默认设备（可能是扬声器）
- 必须遍历 `WaveOut.DeviceCount` 查找匹配 "CABLE" 的设备号

## 四、Bridge DLL 导出函数清单

| 函数名                                  | 用途                             |
| ------------------------------------ | ------------------------------ |
| `Bridge_Init`                        | 初始化声网引擎                        |
| `Bridge_SetRecordingDevice`          | **(未使用)** 用 MMDevice ID 设置录音设备 |
| `Bridge_SetRecordingDeviceById`      | **★ 使用中** 用 Agora 设备 ID 设置     |
| `Bridge_SetAINSMode`                 | 启用/禁用 AI 降噪 + 选择模式             |
| `Bridge_JoinChannel`                 | 加入频道（启动音频采集）                   |
| `Bridge_LeaveChannel`                | 离开频道                           |
| `Bridge_RegisterAudioObserver`       | 注册 onRecordAudioFrame 观察者      |
| `Bridge_RegisterAudioCallback`       | 注册 C# → C++ 的 PCM 回调           |
| `Bridge_Release`                     | 释放引擎                           |
| `Bridge_LoadExtension`               | **(未使用)** 手动加载 AI 降噪扩展         |
| `Bridge_GetSdkVersion`               | 获取 SDK 版本                      |
| `Bridge_GetRecordingDeviceCount`     | 获取 Agora 录音设备数量                |
| `Bridge_GetRecordingDeviceInfo`      | 获取指定索引的设备名和 ID                 |
| `Bridge_FollowSystemRecordingDevice` | **★ 关键** 禁止跟随系统默认设备            |
| `Bridge_GetRecordingDevice`          | 查询 SDK 当前录音设备 ID               |

## 五、开发环境 & 构建

- **Bridge DLL**: 运行 `src\NoiseReduction.Bridge\build.bat`（需要 VS Build Tools 2026, x64）
- **C# 应用**: `dotnet build src\NoiseReduction.App\NoiseReduction.App.csproj`
- **运行**: `dotnet run --project src\NoiseReduction.App\NoiseReduction.App.csproj`
- **依赖**: 声网 SDK DLL (`agora_rtc_sdk.dll` + 扩展 DLL) 在 `res/sdk/x86_64/`，Bridge 构建脚本和 csproj 会自动复制
- **AppID**: `f112df5e7a8b4d529606f8f3bcd7cd8c`（硬编码在 `AgoraAinsPipelineSession.cs`）
- 官方文档:[API 概览 | 文档中心 | 声网](https://doc.shengwang.cn/api-ref/rtc/windows/API/rtc_api_overview)

## 六、未完成工作 & 待改进

1. **用户自定义改名**：用户提出虚拟设备名字太绕（CABLE Input/Output），希望支持用户自定义设备别名。尚未实现。
2. **设备选择持久化**：目前每次启动默认选第一个设备 + CABLE Output。没有记住用户上次选了什么设备。
3. **性能优化**：`OnAudioFrame` 中每帧都 `new byte[]`，有 GC 压力。可改用对象池。
4. **重启降噪**：目前停止后再次点击开始，Bridge DLL 重新加载，引擎重新初始化。可以优化为复用引擎。
5. **Bridge DLL 清理**：`Bridge_SetRecordingDevice`（旧版，用 MMDevice ID）和 `Bridge_LoadExtension` 未被使用，可删除。
6. **多通道支持**：CABLE In/Out 16ch 设备没有被充分利用，所有音频都走标准 2ch 版本。
7. **SdkPoC 清理**：`src/NoiseReduction.SdkPoC` 目录只有编译产物，可以删除或添加源码。

