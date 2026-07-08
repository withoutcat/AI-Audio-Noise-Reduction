using System.Diagnostics;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using NoiseReduction.App.ViewModels;
using NoiseReduction.Core.Logging;

namespace NoiseReduction.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();

        if (DataContext is MainViewModel vm)
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
            LogLevel.Debug => Color.FromRgb(0x6A, 0x73, 0x7D),
            LogLevel.Warn  => Color.FromRgb(0xF0, 0xC0, 0x40),
            LogLevel.Error => Color.FromRgb(0xE7, 0x4C, 0x3C),
            _              => Color.FromRgb(0x20, 0x20, 0x20)
        };

        var paragraph = new Paragraph
        {
            Margin = new Thickness(0),
            Padding = new Thickness(0),
            LineHeight = 1
        };
        paragraph.Inlines.Add(new Run(entry.Message) { Foreground = new SolidColorBrush(color) });
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
            // Only allow focus (and thus text selection) when debug mode is on
            LogRichTextBox.Focusable = vm.DebugMode;
            LogRichTextBox.Cursor = vm.DebugMode ? Cursors.IBeam : Cursors.Arrow;

            // If debug mode was just turned OFF and the RTB has focus, move focus away
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
