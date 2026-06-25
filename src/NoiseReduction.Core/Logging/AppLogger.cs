using System.Collections.ObjectModel;
using System.IO;

namespace NoiseReduction.Core.Logging;

public enum LogLevel
{
    Verbose,
    Info,
    Debug
}

public sealed class AppLogger
{
    private readonly object _lock = new();
    private readonly ObservableCollection<LogEntry> _entries = new();
    private readonly string? _logFilePath;
    private LogLevel _minLevel = LogLevel.Info;

    public AppLogger(string? logFilePath = null)
    {
        _logFilePath = logFilePath;
    }

    public IReadOnlyList<LogEntry> Entries
    {
        get { lock (_lock) return _entries.ToList(); }
    }

    public LogLevel MinLevel
    {
        get => _minLevel;
        set => _minLevel = value;
    }

    public void Verbose(string message)
    {
        Log(LogLevel.Verbose, message);
    }

    public void Info(string message)
    {
        Log(LogLevel.Info, message);
    }

    public void Debug(string message)
    {
        Log(LogLevel.Debug, message);
    }

    private void Log(LogLevel level, string message)
    {
        if (level < _minLevel) return;

        var entry = new LogEntry(DateTime.Now, level, message);
        lock (_lock)
        {
            _entries.Add(entry);
            // Keep max 500 entries
            while (_entries.Count > 500)
            {
                _entries.RemoveAt(0);
            }

            // Write to file log
            if (_logFilePath != null)
            {
                try
                {
                    var line = $"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss}] [{entry.Level}] {entry.Message}";
                    File.AppendAllText(_logFilePath, line + Environment.NewLine);
                }
                catch
                {
                    // Best-effort: don't crash if file logging fails
                }
            }
        }

        EntryAdded?.Invoke(this, entry);
    }

    public void Clear()
    {
        lock (_lock)
        {
            _entries.Clear();
        }
        Cleared?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler<LogEntry>? EntryAdded;
    public event EventHandler? Cleared;
}

public sealed record LogEntry(DateTime Timestamp, LogLevel Level, string Message);
