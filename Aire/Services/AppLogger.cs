using System;
using System.Diagnostics;
using System.IO;

namespace Aire.Services;

/// <summary>
/// Lightweight diagnostic logger. Writes to the Debug output window (visible in Visual
/// Studio Output and tools like DebugView) and to a rolling log file in LocalAppData.
/// No external dependencies — suitable for use anywhere in the process.
/// </summary>
public static class AppLogger
{
    private static readonly string _logPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Aire", "aire.log");

    /// <summary>Logs a diagnostic message at informational level.</summary>
    public static void Info(string context, string message)
        => Write("INFO", context, message, exception: null);

    /// <summary>Logs a warning — something unexpected but recoverable.</summary>
    public static void Warn(string context, string message, Exception? exception = null)
        => Write("WARN", context, message, exception);

    /// <summary>Logs a caught exception that was handled gracefully.</summary>
    public static void Error(string context, string message, Exception? exception = null)
        => Write("ERROR", context, message, exception);

    // ── internals ─────────────────────────────────────────────────────────────

    private static void Write(string level, string context, string message, Exception? exception)
    {
        try
        {
            var line = $"[{DateTime.Now:HH:mm:ss.fff}] [{level}] [{context}] {message}";
            if (exception != null)
                line += $" — {exception.GetType().Name}: {exception.Message}";

            Debug.WriteLine(line);

            // Best-effort file write — never throw from a logger
            var dir = Path.GetDirectoryName(_logPath);
            if (dir != null) Directory.CreateDirectory(dir);
            File.AppendAllText(_logPath, line + Environment.NewLine);
        }
        catch
        {
            // Logger must never crash the application
        }
    }
}
