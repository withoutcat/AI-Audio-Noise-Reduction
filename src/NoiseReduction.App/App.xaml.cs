using System.Windows;
using System.Threading;
using System.IO;
using System.Runtime.InteropServices;
using NoiseReduction.App.ViewModels;
using NoiseReduction.Core.Logging;
using WF = System.Windows.Forms;

namespace NoiseReduction.App;

public partial class App : System.Windows.Application
{
    private static readonly Mutex _mutex = new(true, "NoiseReductionApp_5F3A2B1C");

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr AddDllDirectory(string lpPathName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetDefaultDllDirectories(uint directoryFlags);

    private const uint LOAD_LIBRARY_SEARCH_DEFAULT_DIRS = 0x00001000;

    // ── Window & tray management ─────────────────────────────────────
    private MainWindow? _mainWindow;
    private MiniBarWindow? _miniBarWindow;
    private WF.NotifyIcon? _notifyIcon;

    /// <summary>Shared ViewModel, created once at startup.</summary>
    public MainViewModel ViewModel { get; private set; } = null!;

    public bool IsExiting { get; private set; }
    public bool InstallerLaunched { get; set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        // ── Initialize logging first (global logger available from now on) ──
        AppLogger.Initialize();
        AppLogger.Instance.Info("应用程序启动");

        // ── Global exception handlers ──
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
                AppLogger.Instance.Error(ex, "未处理的应用程序级异常（进程即将终止）");
            else
                AppLogger.Instance.Error($"未处理的应用程序级异常（进程即将终止）: {args.ExceptionObject}");
        };

        DispatcherUnhandledException += (_, args) =>
        {
            AppLogger.Instance.Error(args.Exception, "未处理的UI线程异常");
            args.Handled = true;
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            AppLogger.Instance.Error(args.Exception, "未观察的Task异常");
            args.SetObserved();
        };

        // Allow system shutdown / logoff to close windows normally
        SessionEnding += (_, _) => IsExiting = true;

        // Add native\ to the process DLL search path so NativeLibrary.Load / LoadLibraryEx
        // can find native DLLs (agora_rtc_sdk.dll, etc.) and resolve their transitive dependencies
        var nativeDir = Path.Combine(AppContext.BaseDirectory, "native");
        if (Directory.Exists(nativeDir))
        {
            SetDefaultDllDirectories(LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);
            AddDllDirectory(nativeDir);
        }

        // Prevent multiple instances
        if (!_mutex.WaitOne(TimeSpan.Zero, true))
        {
            System.Windows.MessageBox.Show("AI Noise Reduction 已运行中。", "提示",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            Shutdown();
            return;
        }

        // ── Create shared ViewModel & windows ──
        ViewModel = new MainViewModel();
        _mainWindow = new MainWindow();
        _miniBarWindow = new MiniBarWindow();
        MainWindow = _mainWindow;

        CreateNotifyIcon();

        _mainWindow.Closing += OnWindowClosing;
        _miniBarWindow.Closing += OnWindowClosing;

        base.OnStartup(e);
        _mainWindow.Show();
    }

    // ── Window switching ─────────────────────────────────────────────

    public void ShowMainWindow()
    {
        if (_miniBarWindow != null && _miniBarWindow.IsVisible)
        {
            _mainWindow!.Left = _miniBarWindow.Left;
            _mainWindow.Top = _miniBarWindow.Top + (_miniBarWindow.Height - _mainWindow.Height) / 2;
            _miniBarWindow.Hide();
        }

        if (_mainWindow != null)
        {
            _mainWindow.Show();
            _mainWindow.Activate();
            if (_mainWindow.WindowState == WindowState.Minimized)
                _mainWindow.WindowState = WindowState.Normal;
        }
    }

    public void ShowMiniBar()
    {
        if (_mainWindow != null && _mainWindow.IsVisible)
        {
            _miniBarWindow!.Left = _mainWindow.Left;
            _miniBarWindow.Top = _mainWindow.Top + (_mainWindow.Height - _miniBarWindow.Height) / 2;
            _mainWindow.Hide();
        }

        if (_miniBarWindow != null)
        {
            _miniBarWindow.Show();
            _miniBarWindow.Activate();
        }
    }

    public void MinimizeToTray()
    {
        _mainWindow?.Hide();
        _miniBarWindow?.Hide();
    }

    public void RestoreFromTray()
    {
        // Restore whichever window mode was last active
        if (_mainWindow != null && _mainWindow.IsVisible)
        {
            _mainWindow.Activate();
            if (_mainWindow.WindowState == WindowState.Minimized)
                _mainWindow.WindowState = WindowState.Normal;
        }
        else if (_miniBarWindow != null && _miniBarWindow.IsVisible)
        {
            _miniBarWindow.Activate();
        }
        else
        {
            // Neither is visible — show main window by default
            ShowMainWindow();
        }
    }

    public void ExitApplication()
    {
        IsExiting = true;
        Shutdown();
    }

    // ── Tray icon ────────────────────────────────────────────────────

    private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (IsExiting) return;

        if (InstallerLaunched)
        {
            // Installer is waiting - graceful shutdown
            IsExiting = true;

            // Stop noise reduction if running, before cleanup
            ViewModel.ForceStop();

            // Clean up tray icon and exit
            ExitApplication();
            return;
        }

        // User closed the window -> minimize to tray
        e.Cancel = true;
        MinimizeToTray();
    }

    private void CreateNotifyIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "application.ico");
        var icon = File.Exists(iconPath)
            ? new System.Drawing.Icon(iconPath)
            : System.Drawing.SystemIcons.Application;

        var contextMenu = new WF.ContextMenuStrip();
        contextMenu.Items.Add("显示窗口", null, (_, _) => Dispatcher.Invoke(RestoreFromTray));
        contextMenu.Items.Add(new WF.ToolStripSeparator());
        contextMenu.Items.Add("退出", null, (_, _) => Dispatcher.Invoke(ExitApplication));

        _notifyIcon = new WF.NotifyIcon
        {
            Icon = icon,
            Text = "AI Noise Reduction",
            ContextMenuStrip = contextMenu,
            Visible = true
        };

        _notifyIcon.DoubleClick += (_, _) => Dispatcher.Invoke(RestoreFromTray);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        ViewModel?.Dispose();

        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }

        AppLogger.Instance.Info("应用程序退出");

        _mutex.ReleaseMutex();
        _mutex.Dispose();
        base.OnExit(e);
    }
}