using System.Windows;
using System.Windows.Controls;
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

    private void OnAppIdClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.OpenAppIdDialog();
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
