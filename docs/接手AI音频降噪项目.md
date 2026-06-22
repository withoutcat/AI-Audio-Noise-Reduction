# AI Audio Noise Reduction — 项目交接文档

## 项目概述

Windows 桌面应用，通过**声网（Agora/Shengwang）SDK** 实现实时 AI 音频降噪。
物理麦克风采集的音频经声网SDK降噪后，通过虚拟音频设备（VB-CABLE）输出，
供其他应用程序（腾讯会议、游戏、浏览器等）作为麦克风输入使用。

**技术栈**：C# / .NET 10 / WPF / NAudio / C++（桥接 DLL）

---

## 降噪流程（详细）

```
物理麦克风
    ↓
声网 SDK 采集（通过 setRecordingDeviceById 指定设备）
    ↓ 16kHz mono PCM
声网 AI 降噪引擎（setAINSMode）
    ↓ 16kHz mono PCM（降噪后）
onRecordAudioFrame 回调（C++ → C# 互调）
    ↓
OnAudioFrame() 手动升采样 + 声道复制
  ┌ 输入: 16kHz mono, 1024 samples/frame, 16-bit PCM
  ├ 输出: 48kHz stereo, 3072 samples/frame, 16-bit PCM
  ├ 方式: sample repetition (每个 sample 复制 3 次)
  └ 声道: mono→stereo (2ch，数据相同)
    ↓
BufferedWaveProvider（延迟 500ms）
    ↓
WaveOutEvent / WasapiOut
    ↓
CABLE Input（VB-CABLE 虚拟设备）
    ↓
CABLE Output → 其他应用（作为麦克风输入）
```

### 关键参数

| 参数 | 值 |
|------|-----|
| SDK 输出格式 | 16kHz, mono, 16-bit PCM |
| 输出格式 | 48kHz, stereo, 16-bit PCM |
| 升采样比 | 3x（16→48kHz） |
| 帧大小 | 1024 samples（SDK）→ 3072 samples（输出） |
| Buffer 延迟 | 500ms |
| AI 降噪模式 | 均衡(0) / 强力(1) / 超低延迟(2) |

---

## 工程结构

```
AI-audio-noise-reduction/
├── .gitignore
├── README.md
├── Directory.Build.props          # 通用 MSBuild 属性
├── NuGet.Config
├── docs/
│   └── poc/phase-1-poc.md
├── tools/
│   └── check_exports.ps1          # 检查 Bridge DLL 导出函数
├── res/sdk/Shengwang_Native_SDK_for_Windows_FULL/
│   └── sdk/                       # 声网 SDK (vendored)
│       ├── x86/                   # 32-bit DLLs
│       ├── x86_64/                # 64-bit DLLs
│       └── high_level_api/include/ # C++ 头文件
│
└── src/
    ├── NoiseReduction.Core/       # 纯 C# 接口 / 抽象层
    │   ├── Audio/
    │   │   ├── AudioFrame.cs       # 音频帧数据结构
    │   │   └── AudioFormatSpec.cs  # 音频格式描述
    │   ├── Devices/
    │   │   ├── IAudioDeviceManager.cs  # 设备管理器接口
    │   │   ├── AudioDeviceInfo.cs      # 设备信息 record
    │   │   └── AudioDeviceFlow.cs      # 设备流向枚举
    │   ├── Logging/
    │   │   └── AppLogger.cs        # 线程安全日志系统
    │   └── Pipeline/
    │       └── IAudioPipelineSession.cs  # 音频会话接口
    │
    ├── NoiseReduction.Infrastructure/  # 实现层
    │   ├── Devices/
    │   │   └── NaudioDeviceManager.cs   # NAudio 设备枚举
    │   └── Pipeline/
    │       └── AgoraAinsPipelineSession.cs  # 声网 AI 降噪管线（核心）
    │
    ├── NoiseReduction.App/         # WPF 应用程序
    │   ├── App.xaml(.cs)           # 单实例 Mutex + 全局资源
    │   ├── MainWindow.xaml(.cs)    # 主窗口
    │   ├── Services/
    │   │   ├── AppConfig.cs        # JSON 配置持久化
    │   │   ├── AudioDeviceUtility.cs  # COM 设置默认音频设备
    │   │   └── UiHelper.cs        # Tooltip 辅助
    │   ├── ViewModels/
    │   │   ├── MainViewModel.cs    # 主逻辑（MVVM）
    │   │   └── RelayCommand.cs     # ICommand 实现
    │   └── Views/
    │       └── AppIdDialog.xaml(.cs)  # AppID 验证对话框
    │
    └── NoiseReduction.Bridge/      # C++ 桥接 DLL
        ├── bridge.cpp              # 声网 SDK 的 C 风格封装
        └── build.bat               # MSVC 编译脚本
```

---

## 关键文件详解

### AgoraAinsPipelineSession.cs（核心引擎）

**功能**：管理声网 SDK 的完整生命周期。

**Start() 流程**：
1. 加载 `NoiseReduction.Bridge.dll`
2. 解析所有 C 函数指针（Init / Join / Leave / SetAINS / RegisterObserver 等）
3. NAudio WaveOut 初始化（优先 WaveOutEvent，失败降级 WasapiOut）
4. 声网 SDK 初始化
5. Join（初始化音频模块 SSP）→ Leave → SetRecordingDeviceById → 再 Join
6. 启用 AI 降噪（setAINSMode）
7. 启动音频输出

**回调处理**：`OnAudioFrame()` 由 SDK 每帧调用，执行 PCM 格式转换 + 写入 BufferedWaveProvider。

**运行时切换**：
- `SetAinsMode(int mode)` — 运行时切换降噪等级
- `ChangeCaptureDevice(AudioDeviceInfo device)` — 运行时切换麦克风

**AppID 验证**：
- `VerifyAppId(string appId)` — 静态方法，独立加载 DLL 验证 AppID

### MainViewModel.cs（界面逻辑）

**核心职责**：
- MVVM ViewModel，绑定到 MainWindow
- 管理设备列表、降噪等级、AppID、调试模式
- 异步启动/停止降噪管线
- 配置持久化（AppConfig）
- 默认麦克风自动切换（AudioDeviceUtility）

**关键属性**：
- `CaptureDevices` — 麦克风列表
- `SelectedCaptureDevice` — 当前选择
- `AinsMode` — 降噪等级（0/1/2）
- `AppId` / `HasAppId` — AppID 管理
- `IsRunning` / `_isActive` — 运行状态
- `ShowDeviceWarning` — 默认设备警告
- `StartButtonTooltip` — 动态 Tooltip

### AppConfig.cs（配置）

**存储位置**：`{安装目录}/config.json`
**字段**：
```json
{
  "AppId": "...",
  "LastCaptureDeviceName": "...",
  "LastAinsMode": 0,
  "DebugMode": false,
  "VirtualMicphoneName": "CABLE Output"
}
```
`VirtualMicphoneName` 为未来设备改名预留，安装程序生成唯一名称写入此字段。

### AudioDeviceUtility.cs（系统设备设置）

通过 `IPolicyConfig` COM 接口（Windows Vista+）设置/恢复默认录音设备。
在 `StartAsync()` 中调用，失败不阻塞流程。

### 日志系统

```
LogLevel Verbose → Info → Debug
         ↑调试模式    ↑始终显示   ↑调试模式
```

`OnLogEntryAdded()` 在 ViewModel 中根据 `_debugMode` 过滤。
每 2 秒 `CheckDefaultDevice()` 检测默认麦克风是否改变。

---
## 官网文档

官网浏览API文档需要链接跳转，如果AI无法自主跳转链接遍历所有链接的话，可以提出要求，由人类帮忙给出链接地址

https://doc.shengwang.cn/api-ref/rtc/windows/API/rtc_api_overview

---

## 声网 SDK 集成

### AppID

- 硬编码已移除，改为配置文件传入
- `VerifyAppId()` 验证流程：加载 Bridge DLL → Init → GetSdkVersion → Release
- 验证成功自动关闭对话框并持久化

### Bridge DLL

C++ 封装层 (`NoiseReduction.Bridge.dll`)，隐藏声网 SDK 复杂性：

| 导出函数 | 作用 |
|---------|------|
| `Bridge_Init` | 创建并初始化 RTC 引擎 |
| `Bridge_JoinChannel` | 加入频道 |
| `Bridge_LeaveChannel` | 离开频道 |
| `Bridge_SetAINSMode` | 设置 AI 降噪模式 |
| `Bridge_RegisterAudioObserver` | 注册音频观测者（启用回调） |
| `Bridge_RegisterAudioCallback` | 注册 PCM 回调函数 |
| `Bridge_SetRecordingDeviceById` | 指定录音设备 |
| `Bridge_FollowSystemRecordingDevice` | 是否跟随系统默认设备 |
| `Bridge_GetRecordingDevice` | 查询当前录音设备 |
| `Bridge_GetRecordingDeviceCount` | 枚举录音设备数量 |
| `Bridge_GetRecordingDeviceInfo` | 枚举录音设备名称+ID |
| `Bridge_GetSdkVersion` | 获取 SDK 版本 |
| `Bridge_Release` | 释放引擎 |

---

## 构建与运行

### 前置依赖

1. Visual Studio 2022+（含 C++ 桌面开发工作负载）—— 编译 Bridge DLL
2. .NET 10 SDK
3. VB-CABLE Virtual Audio Device（[下载](https://vb-audio.com/Cable/)）

### 构建步骤

```powershell
# 1. 编译 C++ 桥接 DLL
cd src\NoiseReduction.Bridge
.\build.bat

# 2. 编译 C# 项目
cd ..\..
dotnet build src\NoiseReduction.App

# 3. 运行
dotnet run --project src\NoiseReduction.App
```

### 调试模式

勾选日志面板右上角的"调试"复选框可查看技术细节日志（Verbose + Debug 级别）。

---

## 已知问题与待办

### 高优先级

| 事项 | 说明 |
|------|------|
| 虚拟设备自定义名称 | 见下 |
| 安装包制作 | 需制作安装程序，打包应用 + VB-CABLE 驱动 |
| README 完善 | 英文开源项目 README 已初步完成 |

### 虚拟设备改名方案（等待实施）

不修改 VB-CABLE 源码，改用 Windows `IPropertyStore` API 在安装时重命名：

1. 安装程序安装 VB-CABLE 驱动
2. 通过 `MMDevice.PropertyStore` 修改设备 FriendlyName
3. 生成唯一哈希后缀（如 `"CABLE Output A3F8"`）+ 时间戳
4. 写入 `config.json` 的 `VirtualMicphoneName` 字段
5. 代码中所有 `"CABLE Output"` 硬编码已替换为 `_config.VirtualMicphoneName`

### 移除的冗余代码（已清理）

- `NoiseReduction.Core/Denoise/IDenoiseProcessor.cs` — 旧的直通降噪接口
- `NoiseReduction.Core/Denoise/DenoiseMode.cs` — 未使用的枚举
- `NoiseReduction.Infrastructure/Denoise/PassthroughDenoiseProcessor.cs` — 旧直通实现
- `NoiseReduction.Infrastructure/Pipeline/WasapiPassthroughSession.cs` — 旧非降噪管线

---

## 界面截图与布局

（略——建议实际运行后截图补充）

---

## 设计决策记录

| 决策 | 原因 |
|------|------|
| WPF 而非 WinUI3 | .NET 10 兼容性 + MVVM 成熟度 |
| WindowChrome 无标题栏 | 紧凑 UI 风格，自定义 × 关闭按钮 |
| 固定窗口尺寸 | 元素少，无需自适应 |
| Mutex 单实例 | 防止重复启动 |
| C++ Bridge 而非直接 P/Invoke | 隔离声网 SDK 复杂性 |
| WasapiOut 回退 | WaveOut 对虚拟设备兼容性更好 |
