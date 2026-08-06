using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using NoiseReduction.App.ViewModels;
using NoiseReduction.App.Services;
using Point = System.Windows.Point;

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
    if (e.LeftButton != MouseButtonState.Pressed)
      return;

    try
    {
      DragMove();
    }
    catch (InvalidOperationException)
    {
      // DragMove can throw (e.g. when mouse capture is lost); position did not change, nothing to record
      return;
    }

    // Drag ended: remember the mini bar position so it can be restored when switching back
    if (WindowState == WindowState.Normal)
      WindowPositionStore.LastMiniBarPosition = new Point(Left, Top);
  }

  private void OnTopMostClick(object sender, RoutedEventArgs e)
  {
    if (DataContext is MainViewModel vm)
    {
      vm.ToggleTopMost();
      Topmost = vm.IsTopMost;
    }
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
