// AeroDial — Logger.cs
// Thread-safe, append-only file logger. Deliberately lightweight —
// no NuGet dependency required for something this simple.
//
// Writes are queued and flushed by a background loop every 500 ms. FlushNow()
// drains the queue synchronously so a FATAL line reaches disk before the
// process dies — without it, crash entries were lost in the queue.

using System.Collections.Concurrent;

namespace AeroDial.Core;

internal static class Logger
{
    private static readonly ConcurrentQueue<string> _queue = new();
    private static readonly CancellationTokenSource _cts   = new();
    private static readonly object _writerLock = new();
    private static readonly StreamWriter? _writer;
    private static readonly Task _flushTask;

    static Logger()
    {
        Directory.CreateDirectory(AppConstants.AppDataDir);
        _writer    = TryOpenWriter();
        _flushTask = Task.Run(FlushLoopAsync);
    }

    private static volatile bool _debugEnabled;

    // ── Public API ────────────────────────────────────────────────────────

    public static void SetDebugMode(bool enabled) => _debugEnabled = enabled;

    public static void Info (string msg, Exception? ex = null) => Write("INFO ", msg, ex);
    public static void Warn (string msg, Exception? ex = null) => Write("WARN ", msg, ex);
    public static void Error(string msg, Exception? ex = null) => Write("ERROR", msg, ex);
    public static void Debug(string msg, Exception? ex = null) { if (_debugEnabled) Write("DEBUG", msg, ex); }

    /// <summary>Logs at FATAL level and flushes synchronously — the process may be about to die.</summary>
    public static void Fatal(string msg, Exception? ex = null)
    {
        Write("FATAL", msg, ex);
        FlushNow();
    }

    /// <summary>Drains every queued line to disk on the calling thread.</summary>
    public static void FlushNow()
    {
        lock (_writerLock) DrainLocked();
    }

    // ── Implementation ────────────────────────────────────────────────────

    private static void Write(string level, string msg, Exception? ex)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var line = $"[{timestamp}] [{level}] {msg}";
        if (ex is not null)
            line += Environment.NewLine + "  " + ex.ToString().Replace("\n", "\n  ");

        _queue.Enqueue(line);

#if DEBUG
        System.Diagnostics.Debug.WriteLine(line);
#endif
    }

    private static StreamWriter? TryOpenWriter()
    {
        try
        {
            var stream = new FileStream(AppConstants.LogPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            return new StreamWriter(stream) { AutoFlush = false };
        }
        catch
        {
            return null; // log file unwritable — logging becomes a no-op rather than crashing
        }
    }

    /// <summary>Must be called under _writerLock.</summary>
    private static void DrainLocked()
    {
        if (_writer is null) return;
        try
        {
            while (_queue.TryDequeue(out var line))
                _writer.WriteLine(line);
            _writer.Flush();
        }
        catch { /* never let logging take the app down */ }
    }

    private static async Task FlushLoopAsync()
    {
        while (!_cts.Token.IsCancellationRequested)
        {
            lock (_writerLock) DrainLocked();
            await Task.Delay(500, _cts.Token).ContinueWith(_ => { });
        }
    }
}
