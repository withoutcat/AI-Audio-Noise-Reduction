using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace NoiseReduction.App;

public partial class MiniBarWindow : Window
{
    public MiniBarWindow()
    {
        InitializeComponent();
        DataContext = (System.Windows.Application.Current as App)?.ViewModel;
    }

    private void OnDragMove(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    private void OnExpandClick(object sender, RoutedEventArgs e)
    {
        (System.Windows.Application.Current as App)?.ShowMainWindow();
    }

    private void OnMinimizeToTrayClick(object sender, RoutedEventArgs e)
    {
        (System.Windows.Application.Current as App)?.MinimizeToTray();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        var app = System.Windows.Application.Current as App;
        if (app != null && !app.IsExiting)
        {
            e.Cancel = true;
            app.MinimizeToTray();
        }
        base.OnClosing(e);
    }
}
