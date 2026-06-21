using System.Windows;
using System.Threading;

namespace NoiseReduction.App;

public partial class App : Application
{
    private static readonly Mutex _mutex = new(true, "NoiseReductionApp_5F3A2B1C");

    protected override void OnStartup(StartupEventArgs e)
    {
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
