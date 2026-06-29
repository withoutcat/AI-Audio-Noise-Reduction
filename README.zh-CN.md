# AI Audio Noise Reduction

[English](./README.md) · **简体中文**

<p align="center">
  <strong>Windows 平台实时 AI 音频降噪工具</strong><br/>
  采集麦克风音频，通过声网 SDK 进行深度学习降噪，输出干净音频至虚拟声卡 —— 可供任何应用程序使用。
</p>

<p align="center">
  <img src="https://img.shields.io/badge/.NET-10.0-512BD4" alt=".NET 10.0" />
  <img src="https://img.shields.io/badge/平台-Windows-0078D6?logo=windows" alt="Windows" />
  <img src="https://img.shields.io/badge/许可证-MIT-yellow" alt="MIT License" />
</p>

<p align="center">
  <a href="https://github.com/withoutcat/AI-Audio-Noise-Reduction/releases"><img src="https://img.shields.io/github/v/release/withoutcat/AI-Audio-Noise-Reduction?label=%E4%B8%8B%E8%BD%BD&color=green" alt="下载" /></a>
</p>

---

## 🚀 快速开始

1. 从 [Releases](https://github.com/withoutcat/AI-Audio-Noise-Reduction/releases) 下载最新安装包
2. 运行安装程序（VB-CABLE 和 .NET Runtime 如缺失将自动安装）
3. 启动应用，选择麦克风，选择降噪模式，点击 **开始**
4. 在其他应用（Teams、Zoom、浏览器等）中，选择 **CABLE Output** 作为麦克风

完成！你的声音现在清晰无比。

---

## ✨ 功能特性

- **实时 AI 降噪** — 使用声网 AI 降噪引擎，延迟低于 100ms
- **3 种降噪模式** — 平衡 / 激进 / 超低延迟
- **热切换** — 运行中可随时更换麦克风或降噪模式
- **智能安装** — 安装程序自动检测并安装 VB-CABLE 和 .NET Runtime
- **AppID 管理** — 通过对话框验证并持久化保存声网 AppID
- **默认麦克风切换** — 启动时可自动设置 CABLE Output 为系统默认，停止时恢复
- **状态持久化** — 记忆上次使用的设备、模式和 AppID
- **简洁 UI** — 无边框窗口，自定义标题栏
- **单实例运行** — 互斥锁防止重复启动
- **调试模式** — 切换查看详细技术日志

---

## 🏗️ 工作原理

```mermaid
graph LR
    A[麦克风<br/>选择]
    --> C[AI降噪处理<br/>AI Noise Reduction]
    C --> D[虚拟麦克风<br/>VB-CABLE]
    D --> E[任何应用中选择虚拟麦克风<br/>腾讯会议，浏览器，游戏 等]
```

| 阶段 | 详情 |
|------|------|
| **采集** | 从 UI 选择任意物理麦克风 |
| **降噪** | 声网 AI 噪声抑制（3 种模式） |
| **转换** | 16kHz 单声道 → 48kHz 立体声 |
| **输出** | 写入 VB-CABLE Input → CABLE Output 成为干净的"麦克风" |

---

## 📦 安装

### 使用安装程序（推荐）

从 [Releases](https://github.com/withoutcat/AI-Audio-Noise-Reduction/releases) 下载 `AI_Noise_Reduction_Setup_1.0.0.exe` 并运行。

安装程序将：
- ✅ 安装主应用程序
- ✅ 检测并按需安装 .NET Desktop Runtime 10.0
- ✅ 检测 VB-CABLE 并引导安装
- ✅ 创建桌面快捷方式和开始菜单项

### 系统要求

- **Windows 10 / 11** (x64)
- **[VB-CABLE 虚拟声卡](https://vb-audio.com/Cable/)**（免费）— 安装程序自动安装
- **[声网 (Agora) AppID](https://console.shengwang.cn/)** — 免费额度：每月 10,000 分钟

---

## ⚙️ 配置

配置存储于可执行文件同目录的 `config.json`：

```json
{
  "AppId": "你的声网_app_id",
  "LastCaptureDeviceName": "Microphone (Realtek Audio)",
  "LastAinsMode": 0,
  "DebugMode": false,
  "VirtualMicphoneName": "CABLE Output"
}
```

---

## 🛠️ 从源码构建

<details>
<summary>点击展开构建说明</summary>

### 环境要求

- [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
- Visual Studio 2022+ 并安装 **使用 C++ 的桌面开发** 工作负载

### 构建步骤

```powershell
# 1. 克隆仓库
git clone https://github.com/withoutcat/AI-Audio-Noise-Reduction.git
cd AI-Audio-Noise-Reduction

# 2. 构建原生 Bridge DLL
cd src\NoiseReduction.Bridge
.\build.bat
cd ..\..

# 3. 构建并运行 .NET 应用
dotnet run --project src\NoiseReduction.App
```

> **注意**：Bridge DLL 依赖声网 Native SDK（`res/sdk/`），已包含在本仓库中。

### 构建安装包

```powershell
.\build-installer.ps1
```

输出：`installer\output\AI_Noise_Reduction_Setup_1.0.0.exe`

</details>

---

## 📁 项目结构

```
src/
├── NoiseReduction.Core/              # 接口与抽象
│   ├── Audio/                        #   AudioFrame, AudioFormatSpec
│   ├── Devices/                      #   IAudioDeviceManager, AudioDeviceInfo
│   ├── Logging/                      #   AppLogger (线程安全)
│   └── Pipeline/                     #   IAudioPipelineSession
├── NoiseReduction.Infrastructure/    # 实现
│   ├── Devices/                      #   NaudioDeviceManager
│   └── Pipeline/                     #   AgoraAinsPipelineSession (核心)
├── NoiseReduction.App/               # WPF UI (MVVM)
│   ├── Services/                     #   AppConfig, AudioDeviceUtility, UiHelper
│   ├── ViewModels/                   #   MainViewModel, RelayCommand
│   └── Views/                        #   AppIdDialog
└── NoiseReduction.Bridge/            # C++ → 声网 SDK 桥接 (DLL)
```

---

## 🗺️ 开发计划

- [x] 核心 AI 降噪管线
- [x] AppID 管理与验证
- [x] 热切换麦克风 / 降噪模式
- [x] 单实例、无边框窗口、拖拽支持
- [x] 默认麦克风自动切换（COM PolicyConfig）
- [x] 安装包（集成 VB-CABLE 驱动检测）
- [ ] 自定义虚拟设备命名
- [ ] 设备名称自定义 UI

---

## 🙏 致谢

- [声网 / Agora RTC SDK](https://docs.agora.io/cn/) — AI 降噪引擎
- [NAudio](https://github.com/naudio/NAudio) — 音频设备枚举与播放
- [VB-CABLE](https://vb-audio.com/Cable/) — 虚拟音频驱动

---

## 📄 许可证

本项目基于 MIT 许可证开源。
