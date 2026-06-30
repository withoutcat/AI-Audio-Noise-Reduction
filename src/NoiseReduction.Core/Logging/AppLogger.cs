using System.Collections.ObjectModel;
using System.IO;

namespace NoiseReduction.Core.Logging;

public enum LogLevel
{
    Verbose,
    Debug,
    Info,
    Warn,
    Error,
    Fatal
}

public sealed class AppLogger
{
    private readonly object _lock = new();
    private readonly ObservableCollection<LogEntry> _entries = new();
    private readonly string? _logFilePath;
    private LogLevel _minLevel = LogLevel.Info;

    // ── Static / global access ──────────────────────────────────────

    private static AppLogger? _defaultInstance;
    private static readonly object _initLock = new();

    /// <summary>
    /// Gets the default application-wide logger instance.
    /// Must call <see cref="Initialize"/> once at startup before first use.
    /// </summary>
    public static AppLogger Instance =>
        _defaultInstance ?? throw new InvalidOperationException(
            "AppLogger has not been initialized. Call AppLogger.Initialize() at application startup.");

    public static bool IsInitialized => _defaultInstance != null;

    /// <summary>
    /// Initialize the default application-wide logger.
    /// Logs are written to {AppContext.BaseDirectory}/logs/ by default
    /// (i.e. next to the running executable, works for both dotnet run and installed app).
    /// Safe to call multiple times — only the first call takes effect.
    /// </summary>
    public static void Initialize(string? logDirectory = null)
    {
        lock (_initLock)
        {
            if (_defaultInstance != null) return;
    
            var dir = logDirectory ?? Path.Combine(AppContext.BaseDirectory, "logs");
            try
            {
                Directory.CreateDirectory(dir);
                // Test write permission by creating and deleting a temp file.
                // Directory.CreateDirectory can succeed on an existing dir even
                // when the user lacks write access (e.g. Program Files).
                var testFile = Path.Combine(dir, $".wtest-{Guid.NewGuid():N}.tmp");
                File.WriteAllText(testFile, "");
                File.Delete(testFile);
            }
            catch
            {
                // If we can't create the log directory or write to it (e.g. installed under
                // Program Files without elevation), fall back to a temp path.
                dir = Path.Combine(Path.GetTempPath(), "ANR-logs");
                Directory.CreateDirectory(dir);
            }
            var logPath = Path.Combine(dir, $"ANR-{DateTime.Now:yyyyMMdd}.log");
            _defaultInstance = new AppLogger(logPath);
        }
    }

    // ── Instance ────────────────────────────────────────────────────

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

    // ── Logging methods ─────────────────────────────────────────────

    public void Verbose(string message) => Log(LogLevel.Verbose, message);
    public void Debug(string message) => Log(LogLevel.Debug, message);
    public void Info(string message) => Log(LogLevel.Info, message);
    public void Warn(string message) => Log(LogLevel.Warn, message);
    public void Error(string message) => Log(LogLevel.Error, message);
    public void Fatal(string message) => Log(LogLevel.Fatal, message);

    /// <summary>Log an exception at Error level with full stack trace.</summary>
    public void Error(Exception ex, string? message = null)
    {
        Log(LogLevel.Error, FormatException(ex, message));
    }

    /// <summary>Log an exception at Fatal level with full stack trace.</summary>
    public void Fatal(Exception ex, string? message = null)
    {
        Log(LogLevel.Fatal, FormatException(ex, message));
    }

    private static string FormatException(Exception ex, string? message)
    {
        return string.IsNullOrEmpty(message) ? ex.ToString() : $"{message}\n{ex}";
    }

    private void Log(LogLevel level, string message)
    {
        if (level < _minLevel) return;

        var entry = new LogEntry(DateTime.Now, level, message);
        lock (_lock)
        {
            _entries.Add(entry);
            // Keep max 500 entries in memory
            while (_entries.Count > 500)
                _entries.RemoveAt(0);

            // Write to file log (best-effort)
            if (_logFilePath != null)
            {
                try
                {
                    var line = $"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss}] [{entry.Level}] {entry.Message}";
                    File.AppendAllText(_logFilePath, line + Environment.NewLine);
                }
                catch { }
            }
        }

        EntryAdded?.Invoke(this, entry);
    }

    public void Clear()
    {
        lock (_lock) _entries.Clear();
        Cleared?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler<LogEntry>? EntryAdded;
    public event EventHandler? Cleared;
}

public sealed record LogEntry(DateTime Timestamp, LogLevel Level, string Message);
