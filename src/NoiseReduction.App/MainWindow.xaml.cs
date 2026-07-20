using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using NoiseReduction.App.ViewModels;
using NoiseReduction.Core.Logging;
using SWM = System.Windows.Media;
using SWI = System.Windows.Input;

namespace NoiseReduction.App;

public partial class MainWindow : Window
{
  public MainWindow()
  {
    InitializeComponent();

    var app = System.Windows.Application.Current as App;
    var vm = app?.ViewModel;
    DataContext = vm;

    if (vm != null)
    {
      // Append colored log entries to RichTextBox
      vm.LogEntries.CollectionChanged += (s, e) =>
      {
        if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add
                  && e.NewItems?[0] is LogEntry entry)
        {
          Dispatcher.InvokeAsync(() =>
                {
                  AppendLogEntry(entry);
                }, System.Windows.Threading.DispatcherPriority.Loaded);
        }
      };

      // Clear RichTextBox when log is cleared
      vm.LogCleared += () =>
      {
        Dispatcher.Invoke(() =>
              {
                LogRichTextBox.Document.Blocks.Clear();
              });
      };

      // Observe DebugMode changes → toggle selection/copy ability
      vm.PropertyChanged += (s, e) =>
      {
        if (e.PropertyName == nameof(MainViewModel.DebugMode))
        {
          UpdateLogReadOnlyMode();
        }
      };
    }
  }

  private void AppendLogEntry(LogEntry entry)
  {
    var doc = LogRichTextBox.Document;
    var color = entry.Level switch
    {
      LogLevel.Debug => SWM.Color.FromRgb(0x6A, 0xB8, 0xFF),
      LogLevel.Warn => SWM.Color.FromRgb(0xF0, 0xC0, 0x40),
      LogLevel.Error => SWM.Color.FromRgb(0xE7, 0x4C, 0x3C),
      _ => SWM.Color.FromRgb(0x20, 0x20, 0x20)
    };

    var paragraph = new Paragraph
    {
      Margin = new Thickness(0),
      Padding = new Thickness(0),
      LineHeight = 1
    };
    paragraph.Inlines.Add(new Run(entry.Message) { Foreground = new SWM.SolidColorBrush(color) });
    doc.Blocks.Add(paragraph);

    // Trim old entries to keep max 200
    while (doc.Blocks.Count > 200)
      doc.Blocks.Remove(doc.Blocks.FirstBlock);

    LogRichTextBox.ScrollToEnd();
  }

  private void UpdateLogReadOnlyMode()
  {
    if (DataContext is MainViewModel vm)
    {
      LogRichTextBox.Focusable = vm.DebugMode;
      LogRichTextBox.Cursor = vm.DebugMode ? SWI.Cursors.IBeam : SWI.Cursors.Arrow;

      if (!vm.DebugMode && LogRichTextBox.IsFocused)
      {
        LogRichTextBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
      }
    }
  }

  /// <summary>Drag the window by grabbing any blank area.</summary>
  private void OnDragMove(object sender, MouseButtonEventArgs e)
  {
    if (e.LeftButton == MouseButtonState.Pressed)
      DragMove();
  }

  private void OnMiniBarClick(object sender, RoutedEventArgs e)
  {
    (System.Windows.Application.Current as App)?.ShowMiniBar();
  }

  private void OnTopMostClick(object sender, RoutedEventArgs e)
  {
    if (DataContext is MainViewModel vm)
    {
      vm.ToggleTopMost();
      Topmost = vm.IsTopMost;
    }
  }

  private void OnMinimizeToTrayClick(object sender, RoutedEventArgs e)
  {
    (System.Windows.Application.Current as App)?.MinimizeToTray();
  }

  private void OnCloseToTrayClick(object sender, RoutedEventArgs e)
  {
    (System.Windows.Application.Current as App)?.MinimizeToTray();
  }

  private void OnAppIdClick(object sender, MouseButtonEventArgs e)
  {
    if (DataContext is MainViewModel vm)
    {
      vm.OpenAppIdDialog();
    }
  }

  private void OnSelectableCaptureDeviceDropDownOpened(object sender, System.EventArgs e)
  {
    if (DataContext is MainViewModel vm)
    {
      vm.RefreshCaptureDevices();
    }
  }

  private void OnOpenSoundSettings(object sender, RoutedEventArgs e)
  {
    if (Environment.OSVersion.Version.Build >= 22000)
    {
      Process.Start(new ProcessStartInfo
      {
        FileName = "ms-settings:sound-input",
        UseShellExecute = true
      });
    }
    else
    {
      Process.Start("control.exe", "mmsys.cpl,,1");
    }
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
