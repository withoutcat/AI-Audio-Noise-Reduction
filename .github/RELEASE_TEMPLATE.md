# 𝔸 ℕ𝕚𝕤𝕖 ℝ𝕖𝕕𝕦𝕔𝕥𝕚𝕠𝕟 v{{VERSION}}

> 🎙️ Real-time AI-powered audio noise suppression for Windows

---

## ✨ What's New in v{{VERSION}}

🎉 **First official release!** This is the initial stable version of AI Noise Reduction.

### 🔧 Core Features

| Feature | Description |
|---------|-------------|
| 🤖 **AI Denoising** | Real-time noise suppression powered by Agora SDK (sub-100ms latency) |
| 🎚️ **3 Denoising Modes** | Balanced / Aggressive / Ultra-low-latency |
| 🔄 **Hot-switch** | Change microphone or mode while running — no restart needed |
| 🎯 **Default Mic Switching** | Auto-set CABLE Output as system default on start, restore on stop |
| 💾 **State Persistence** | Remembers your last device, mode, and AppID |
|  **Compact UI** | Borderless window with custom title bar, draggable anywhere |
| 🔒 **Single Instance** | Mutex-protected against duplicate launches |
| 🐛 **Debug Mode** | Toggle to see detailed technical logs |

---

## 📦 Installation

### Quick Install (Recommended)

1. Download `AINoiseReduction-{{VERSION}}-win-x64.exe` below
2. Run the installer — it will:
   - ✅ Install the main application
   - ✅ Check & install .NET Desktop Runtime 10.0 if missing
   - ✅ Detect VB-CABLE and guide installation if needed
   - ✅ Create desktop shortcut & Start Menu entry
3. Launch the app, select your microphone, choose a denoising mode, click **Start**
4. In any other app (Teams, Zoom, browser), select **CABLE Output** as your microphone

That's it! Your voice is now crystal clear. 🎧

### System Requirements

- **OS**: Windows 10 / 11 (x64)
- **.NET**: Desktop Runtime 10.0 (auto-installed if missing)
- **VB-CABLE**: Free virtual audio driver (guided install if missing)
- **AppID**: [Shengwang (Agora)](https://console.shengwang.cn/) — Free tier: 10,000 min/month

---

## 🏗️ How It Works

```
┌─────────────┐    ┌─────────────────────┐    ┌─────────────┐    ┌─────────────┐
│  Microphone │───▶│ Agora SDK (16kHz)   │───▶│ AI Denoiser │───▶│   Output    │
│   (Select)  │    │  AI Noise Suppress  │    │             │    │  VB-CABLE   │
└─────────────┘    └─────────────────────┘    └─────────────┘    └─────────────┘
                                                                         │
                                                                         ▼
                                                                  Any Application
                                                               (Teams, Zoom, etc.)
```

---

## 📋 Changelog

This section is auto-generated from git commits. See below ↓
