using System.Windows;
using System.Threading;
using System.IO;
using System.Runtime.InteropServices;

namespace NoiseReduction.App;

public partial class App : Application
{
    private static readonly Mutex _mutex = new(true, "NoiseReductionApp_5F3A2B1C");

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetDllDirectory(string lpPathName);

    protected override void OnStartup(StartupEventArgs e)
    {
        // Add native\ subdirectory to DLL search path for Agora SDK dependencies
        var nativeDir = Path.Combine(AppContext.BaseDirectory, "native");
        if (Directory.Exists(nativeDir))
        {
            SetDllDirectory(nativeDir);
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
        _mutex.ReleaseMutex();
        _mutex.Dispose();
        base.OnExit(e);
    }
}
