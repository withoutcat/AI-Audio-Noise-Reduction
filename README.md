# AI Audio Noise Reduction

Real-time AI-powered audio noise suppression for Windows.  
Captures microphone audio, applies deep learning denoising via the Agora SDK, and outputs the cleaned audio through a virtual audio device — ready for use by any application (conference calls, games, browsers).

![Tech Stack](https://img.shields.io/badge/.NET-10.0-512BD4)
![Platform](https://img.shields.io/badge/Platform-Windows-0078D6)
![License](https://img.shields.io/badge/License-MIT-yellow)

---

## How It Works

```
Microphone → Agora SDK (16kHz mono) → AI Denoiser → Format Conversion (48kHz stereo) → Virtual Cable → Any App
```

| Stage | Detail |
|-------|--------|
| **Capture** | Select any physical microphone from the UI |
| **Denoising** | Agora AI Noise Suppression (3 modes: Balanced / Aggressive / Ultra-low-latency) |
| **Conversion** | 16kHz mono → 48kHz stereo via sample repetition |
| **Output** | Writes to VB-CABLE Input → CABLE Output becomes a clean "microphone" for other apps |

## Features

- **Real-time** — sub-100ms latency AI denoising
- **3 denoising levels** — Balanced (daily use), Aggressive (noisy environments), Ultra-low-latency (live scenarios)
- **Hot-switch** — change microphone or denoising mode while running
- **AppID management** — verify and persist Agora AppID via dialog
- **Auto default mic switching** — optionally set CABLE Output as system default on start, restore on stop
- **Persistence** — remembers last device, mode, and AppID in `config.json`
- **Compact UI** — borderless window with custom title bar, draggable by any blank area
- **Single instance** — Mutex-protected against duplicate launches
- **Debug mode** — toggle to see detailed technical logs

## Prerequisites

- Windows 10 / 11 (x64)
- [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
- Visual Studio 2022+ with **Desktop development with C++** workload (to build the native Bridge DLL)
- [VB-CABLE Virtual Audio Device](https://vb-audio.com/Cable/) — free virtual audio driver
- A valid [Shengwang (Agora) AppID](https://console.shengwang.cn/) — free tier includes 10,000 minutes/month

## Build & Run

```powershell
# 1. Build the native Bridge DLL
cd src\NoiseReduction.Bridge
.\build.bat

# 2. Build the .NET app
cd ..\..
dotnet build src\NoiseReduction.App

# 3. Run
dotnet run --project src\NoiseReduction.App
```

> **Note**: The Bridge DLL depends on the Shengwang Native SDK (`res/sdk/`), which is vendored in this repo.

## Project Structure

```
src/
├── NoiseReduction.Core/              # Interfaces & abstractions
│   ├── Audio/                        #   AudioFrame, AudioFormatSpec
│   ├── Devices/                      #   IAudioDeviceManager, AudioDeviceInfo
│   ├── Logging/                      #   AppLogger (thread-safe)
│   └── Pipeline/                     #   IAudioPipelineSession
├── NoiseReduction.Infrastructure/    # Implementations
│   ├── Devices/                      #   NaudioDeviceManager
│   └── Pipeline/                     #   AgoraAinsPipelineSession (core)
├── NoiseReduction.App/               # WPF UI (MVVM)
│   ├── Services/                     #   AppConfig, AudioDeviceUtility, UiHelper
│   ├── ViewModels/                   #   MainViewModel, RelayCommand
│   └── Views/                        #   AppIdDialog
└── NoiseReduction.Bridge/            # C++ → Agora SDK bridge (DLL)
    ├── bridge.cpp
    └── build.bat
```

## Configuration

All settings are stored in `config.json` next to the executable:

```json
{
  "AppId": "your_agora_app_id",
  "LastCaptureDeviceName": "Microphone (Realtek Audio)",
  "LastAinsMode": 0,
  "DebugMode": false,
  "VirtualMicphoneName": "CABLE Output"
}
```

## Roadmap

- [x] Core AI denoising pipeline
- [x] AppID management & verification
- [x] Hot-switch microphone / denoising mode
- [x] Single instance, borderless window, drag support
- [x] Default mic auto-switch (via COM PolicyConfig)
- [ ] Custom virtual device naming (installer renames CABLE with unique suffix)
- [ ] MSI/Installer package (bundles VB-CABLE driver)
- [ ] Device name customization UI

## Acknowledgments

- [Agora / Shengwang RTC SDK](https://docs.agora.io/en/) — AI Noise Suppression engine
- [NAudio](https://github.com/naudio/NAudio) — Audio device enumeration & playback
- [VB-CABLE](https://vb-audio.com/Cable/) — Virtual audio driver
