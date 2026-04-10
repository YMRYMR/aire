using System;
using System.Diagnostics;
using System.IO;

namespace Aire.Services;

/// <summary>
/// Lightweight diagnostic logger for Aire.Core.
/// Writes to Debug output and a rolling log file under LocalAppData.
/// </summary>
internal static class AppLogger
{
    private static readonly string _logPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Aire", "aire.log");

    internal static void Info(string context, string message)
        => Write("INFO", context, message, null);

    internal static void Warn(string context, string message, Exception? exception = null)
        => Write("WARN", context, message, exception);

    internal static void Error(string context, string message, Exception? exception = null)
        => Write("ERROR", context, message, exception);

    private static void Write(string level, string context, string message, Exception? exception)
    {
        try
        {
            var line = $"[{DateTime.Now:HH:mm:ss.fff}] [{level}] [{context}] {message}";
            if (exception != null)
                line += $" — {exception.GetType().Name}: {exception.Message}";

            Debug.WriteLine(line);

            var dir = Path.GetDirectoryName(_logPath);
            if (dir != null)
                Directory.CreateDirectory(dir);

            File.AppendAllText(_logPath, line + Environment.NewLine);
        }
        catch
        {
        }
    }
}
