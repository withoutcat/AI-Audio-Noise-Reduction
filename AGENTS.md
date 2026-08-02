# AGENTS.md - AI Audio Noise Reduction 项目大纲与开发规范

> 本文件用于让 Codex / 各类 AI Agent 在新会话中快速理解本项目，并遵循统一的工作方式与代码规范。
> 当前内容为通用型规范（v1），详细规则由项目维护者后续手动补充完善。
> 维护说明：本文件会被 Agent 自动读取，修改后对新会话立即生效；请保持简洁、准确、与代码同步。

## 1. 项目概述

Windows 桌面应用，实现实时 AI 音频降噪：

- 物理麦克风采集音频 -> 声网（Agora/Shengwang）SDK AI 降噪引擎处理 -> 输出到 VB-CABLE 虚拟声卡 -> 其他应用（会议、游戏、浏览器等）将 CABLE Output 作为"干净"的麦克风使用。
- 单实例运行（Mutex），无边框 WPF 主窗口 + MiniBar 迷你条窗口 + 系统托盘。
- 主要功能：AppID 管理、3 种降噪模式（均衡/强力/超低延迟）、运行时热切换设备与模式、自动切换系统默认麦克风、自动更新（v1.3+，含 SHA256 校验与进度 UI）、日志面板、窗口置顶。

## 2. 技术栈

| 层 | 技术 |
| --- | --- |
| UI / 入口 | C# / .NET 10 / WPF（net10.0-windows，x64） |
| 架构模式 | MVVM（MainViewModel + RelayCommand） |
| 音频 | NAudio（设备枚举与播放）、声网 RTC SDK（AI 降噪，48kHz stereo PCM） |
| 原生桥接 | C++（MSVC）-> NoiseReduction.Bridge.dll，C# 通过 P/Invoke 互调 |
| 系统麦克风切换 | AudioDeviceCmdlets（PowerShell 模块，随应用打包在 native\，非系统级安装） |
| 配置持久化 | System.Text.Json -> %LOCALAPPDATA%\AINoiseReduction\config.json |
| 日志 | 自研 AppLogger（所有级别写入文件，DebugMode 仅控制 UI 面板可见性） |
| 打包 | Inno Setup 6（installer/setup.iss） |
| CI | GitHub Actions（.github/workflows/release.yml） |
| 版本管理 | Git tag 决定版本号（当前最新 v1.3.4） |

## 3. 架构与数据流

### 分层结构

```
NoiseReduction.Core            接口 / 抽象层（音频帧、设备、管道会话、日志）
        |
NoiseReduction.Infrastructure  实现层（NAudio 设备管理、声网降噪会话 AgoraAinsPipelineSession）
        |
NoiseReduction.App             WPF UI（MVVM：MainViewModel 为核心逻辑）
        |
NoiseReduction.Bridge          C++ 桥接 DLL（封装声网原生 SDK，导出 Bridge_* 函数）
```

### 音频数据流

```
物理麦克风 -> 声网 SDK 采集（setRecordingDeviceById）
          -> AI 降噪引擎（setAINSMode：0 均衡 / 1 强力 / 2 超低延迟）
          -> onRecordAudioFrame 回调（48kHz stereo PCM，960 samples/ch，20ms）
          -> C# OnAudioFrame() 直接写入 BufferedWaveProvider（无需格式转换）
          -> WasapiOut（优先）/ WaveOutEvent（降级）
          -> CABLE Input（VB-CABLE）-> CABLE Output -> 其他应用作为麦克风输入
```

## 4. 目录结构与关键文件

```
AI-audio-noise-reduction/
|-- AGENTS.md                       本文件（Agent 项目大纲与规范）
|-- build-installer.ps1             一键构建安装包（参数化）
|-- Directory.Build.props           通用 MSBuild 属性（默认 Version 0.0.0）
|-- README.md / README.zh-CN.md     项目说明（含构建、配置、使用文档）
|-- docs/
|   |-- 接手AI音频降噪项目.md       项目交接文档（可能滞后于代码，以源码为准）
|   |-- 自动切换麦克风功能实施总结.md
|   +-- README_AudioDeviceCmdlets(ADC).md
|-- installer/
|   +-- setup.iss                   Inno Setup 安装脚本
|-- license/                        MIT 许可证 + AudioDeviceCmdlets 许可证
|-- res/
|   |-- sdk/Shengwang_Native_SDK_for_Windows_FULL/   声网原生 SDK（随仓库 vendored）
|   +-- tools/AudioDeviceCmdlets/   ADC 模块源（.dll + .psd1）
|-- src/
|   |-- NoiseReduction.Core/        接口：AudioFrame、AudioFormatSpec、IAudioDeviceManager、
|   |                               IAudioPipelineSession、AppLogger
|   |-- NoiseReduction.Infrastructure/
|   |   |-- Devices/NaudioDeviceManager.cs
|   |   +-- Pipeline/AgoraAinsPipelineSession.cs     核心降噪会话
|   |-- NoiseReduction.App/         WPF 应用
|   |   |-- App.xaml.cs             启动、单实例、托盘、生命周期、AppLogger 初始化
|   |   |-- MainWindow.xaml(.cs)    主窗口
|   |   |-- MiniBarWindow.xaml(.cs) 迷你条窗口
|   |   |-- Services/               AppConfig、AppUpdaterService、AudioDeviceSwitcher、
|   |   |                           AudioDeviceUtility、UiHelper
|   |   |-- ViewModels/             MainViewModel（核心逻辑）、RelayCommand
|   |   +-- Views/AppIdDialog.xaml(.cs)
|   +-- NoiseReduction.Bridge/      C++ 桥接：bridge.cpp + build.bat
+-- tools/check_exports.ps1         检查 Bridge DLL 导出函数
```

### 核心源码速览

| 文件 | 职责 |
| --- | --- |
| App.xaml.cs | 启动入口：先 AppLogger.Initialize()，再注册全局异常处理器；单实例 Mutex；托盘与窗口生命周期；IsExiting / InstallerLaunched |
| MainViewModel.cs | MVVM 核心：设备列表、降噪模式、AppID、自动切换麦克风、自动更新状态、资源监控、日志过滤 |
| AgoraAinsPipelineSession.cs | 声网 SDK 生命周期：加载 Bridge DLL -> 解析函数指针 -> 初始化 -> 采集/输出 -> 降噪 -> 回调写缓冲；支持运行时 SetAinsMode / ChangeCaptureDevice；VerifyAppId() 静态验证 |
| AppUpdaterService.cs | 自动更新：GitHub Releases API、SHA256 校验、流式下载与进度、临时缓存校验 |
| AudioDeviceSwitcher.cs | 通过 AudioDeviceCmdlets（PowerShell，隐藏窗口）切换系统默认麦克风 |
| AppConfig.cs | JSON 配置加载/保存（见第 5 节路径） |
| bridge.cpp | 声网 SDK 的 C 风格封装，导出 Bridge_Init/JoinChannel/SetAINSMode/RegisterAudioCallback/... |

## 5. 运行时路径

| 用途 | 路径 |
| --- | --- |
| 配置文件 | %LOCALAPPDATA%\AINoiseReduction\config.json（AppId、LastUserMicphoneID、LastAinsMode、DebugMode、AutoSwitchMic、DefaultVirtualMicphoneID） |
| 日志文件 | {AppContext.BaseDirectory}\logs\ANR-{yyyyMMdd}.log；不可写时回退 %TEMP%\ANR-logs |
| 更新缓存 | %TEMP%\ANR-update\（校验 ProductName="AI Noise Reduction" 与 ProductVersion） |
| 原生 DLL | 输出目录下 native\（Bridge.dll、agora SDK DLL、AudioDeviceCmdlets.dll） |
| 安装包输出 | installer\output\AINoiseReduction-{version}-win-x64.exe |

## 6. 构建与打包

```powershell
# 1) 编译 C++ 桥接 DLL（必须先做）
cd src\NoiseReduction.Bridge
.\build.bat
cd ..\..

# 2) 构建 / 运行 .NET 应用
dotnet build src\NoiseReduction.App
dotnet run --project src\NoiseReduction.App

# 3) 一键构建安装包（build-installer.ps1 参数化）
.\build-installer.ps1                                    # 版本取自 git tag
.\build-installer.ps1 -AppVersion "1.3.0"                # 指定版本
.\build-installer.ps1 -Configuration Debug -SkipBridge   # Debug + 跳过桥接编译
.\build-installer.ps1 -SkipPublish                       # 跳过 dotnet publish
```

build-installer.ps1 参数：-AppVersion（默认 git tag）、-Configuration（Debug/Release）、-SkipBridge、-SkipPublish。CI 流程见 .github/workflows/release.yml。

## 7. 编码规范（通用版）

> 以下为通用约定，详细规则由维护者逐步补充。新增/修改代码时默认遵守。

- C# 语言：.NET 10 最新语言特性、可空引用类型（Nullable enable）、隐式 using（ImplicitUsings enable）已全局开启。
- MVVM：UI 逻辑放入 MainViewModel，通过 RelayCommand 绑定；视图层不写业务逻辑。属性变更通知统一通过 SetField/OnPropertyChanged 触发，影响所有计算属性时必须逐一通知（如 UpdateAvailable -> DownloadButtonContent/DownloadButtonVisible）。
- 代码简洁：优先自动属性（public bool IsExiting { get; private set; }），避免冗余 backing field；能直接传递的逻辑不做间接包装。
- 日志：AppLogger 所有级别无条件写入文件；DebugMode 只控制 UI 日志面板可见性。Error(Exception, string?) 会生成两条记录（Error 供 UI，Debug 含完整堆栈供文件）。
- 命名：C# 使用 PascalCase；本地变量 camelCase；类名/方法名表意明确；中文注释与 UI 文案保持一致。
- 异步：UI 线程外访问控件/集合需经 Dispatcher.InvokeAsync；耗时操作使用 Task.Run 并保持取消/释放路径完整（IDisposable 会话）。
- 配置：新增可持久化设置需在 AppConfig 增加字段，ViewModel setter 中保存，并保证默认值向后兼容。
- UI：无边框窗口、固定尺寸；控件样式/颜色以现有 XAML 为准（如更新按钮 Background #EDEEF0、BorderBrush #D0D7DE、Foreground #3D444D、Width 50）。

## 8. Agent 工作方式（通用版）

- 动手前先读项目：新功能/修复前先阅读受影响文件及其依赖，必要时通读整个项目；先看本文件 + README + 相关 docs/，再定位源码。
- 需求逐条落实：用户给出的详细需求按清单逐条实现并逐条验证，不得跳过任何一条。
- 外部依赖风险评估：引入新工具/依赖前先评估安全性、侵入性、卸载残留；优先非侵入方案（如项目内打包而非系统级安装）。
- 改动后自验证：编辑文件后搜索确认修改已生效；代码改动后按构建顺序编译（Bridge -> App），确认无真实编译错误（MSB3026 文件占用属环境问题，可忽略）。
- 构建脚本实测：修改构建脚本后用真实参数测试（如 -AppVersion "x.x.x"）。
- Git 卫生：提交前检查 git status，避免误删/误恢复文件（如 NuGet.Config）；不提交 bin/、obj/、installer/output/、日志等（见 .gitignore）；新建分支使用 codex/ 前缀。
- 尊重用户数据：用户提供的接口返回/运行数据优先于假设；不确定时先验证再下结论。

## 9. 已知陷阱

| 陷阱 | 说明 |
| --- | --- |
| AppLogger 初始化顺序 | WPF Application 字段初始化器先于 OnStartup 执行；依赖 AppLogger.Initialize() 的 ViewModel 必须延迟到 OnStartup 中创建（= null!），否则启动崩溃 |
| WPF 绑定不刷新 | 新增属性影响多个计算属性时，未对所有依赖属性调用 OnPropertyChanged |
| C# 多行编辑 | 不要用 Node.js replace() 处理多行 C# 模式（缩进不匹配会静默失败）；用 PowerShell 按行读取/定位/插入 |
| build.bat 的 ^ 续行 | 经 cmd.exe /c 从 PowerShell 调用时 ^ 会被当作字面参数；避免续行写法 |
| 脚本步骤头 | build-installer.ps1 中步骤标题必须以 # 开头，否则会被当作命令执行 |
| 麦克风切换 | 优先用 AudioDeviceCmdlets（Import-Module '<绝对路径>\AudioDeviceCmdlets.dll' + Set-AudioDevice -ID），旧的 COM IPolicyConfig 方案不可靠；PowerShell 调用需隐藏窗口（WindowStyle=Hidden、CreateNoWindow=true）并设超时 |
| 原生 DLL 搜索 | 依赖 AddDllDirectory(native) 解析原生依赖；新增原生 DLL 时确认 csproj 复制规则与 csproj 中 SDK DLL 排除清单一致 |

## 10. 版本与发布

- 版本号来源：git tag（build-installer.ps1 自动读取，可被 -AppVersion 覆盖），程序集版本由 publish 时的 -p:Version= 注入。
- 更新检查端点：https://api.github.com/repos/withoutcat/AI-Audio-Noise-Reduction/releases/latest，依赖资产中的 digest（sha256）字段做完整性校验。
- 发布流程：打 tag -> GitHub Actions（.github/workflows/release.yml）构建安装包 -> 发布 Release。
