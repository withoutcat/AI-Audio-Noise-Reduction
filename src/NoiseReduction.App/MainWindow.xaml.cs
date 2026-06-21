using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using NoiseReduction.App.ViewModels;

namespace NoiseReduction.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();

        // Auto-scroll log TextBox
        if (DataContext is MainViewModel vm)
        {
            vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.LogText))
                {
                    LogTextBox.ScrollToEnd();
                }
            };
        }
    }

    /// <summary>Drag the window by grabbing any blank area.</summary>
    private void OnDragMove(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnAppIdClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.OpenAppIdDialog();
        }
    }

    private void OnOpenSoundSettings(object sender, RoutedEventArgs e)
    {
        if (Environment.OSVersion.Version.Build >= 22000)
        {
            // Windows 11: ms-settings URIs require UseShellExecute=true
            Process.Start(new ProcessStartInfo
            {
                FileName = "ms-settings:sound-input",
                UseShellExecute = true
            });
        }
        else
        {
            // Windows 10 and below: legacy Control Panel → Recording tab
            Process.Start("control.exe", "mmsys.cpl,,1");
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is IDisposable disposable)
        {
            disposable.Dispose();
        }

        base.OnClosed(e);
    }
}
