using System.Windows;
using System.Threading;
using System.IO;
using System.Runtime.InteropServices;
using NoiseReduction.Core.Logging;

namespace NoiseReduction.App;

public partial class App : Application
{
    private static readonly Mutex _mutex = new(true, "NoiseReductionApp_5F3A2B1C");

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr AddDllDirectory(string lpPathName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetDefaultDllDirectories(uint directoryFlags);

    private const uint LOAD_LIBRARY_SEARCH_DEFAULT_DIRS = 0x00001000;

    protected override void OnStartup(StartupEventArgs e)
    {
        // ── Initialize logging first (global logger available from now on) ──
        AppLogger.Initialize();
        AppLogger.Instance.Info("应用程序启动");

        // ── Global exception handlers ──
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
                AppLogger.Instance.Fatal(ex, "未处理的应用程序级异常（进程即将终止）");
            else
                AppLogger.Instance.Fatal($"未处理的应用程序级异常（进程即将终止）: {args.ExceptionObject}");
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
            MessageBox.Show("AI Noise Reduction 已运行中。", "提示",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        AppLogger.Instance.Info("应用程序退出");

        _mutex.ReleaseMutex();
        _mutex.Dispose();
        base.OnExit(e);
    }
}
