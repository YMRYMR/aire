using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static Aire.Services.Tools.ToolHelpers;

namespace Aire.Services.Tools
{
    internal class SystemToolService
    {
#pragma warning disable CA2101
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
#pragma warning restore CA2101

        public static ToolExecutionResult ExecuteGetSystemInfo()
        {
            try
            {
                var gcInfo = GC.GetGCMemoryInfo();
                var totalRam = gcInfo.TotalAvailableMemoryBytes / (1024.0 * 1024 * 1024);
                var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);

                var sb = new StringBuilder();
                sb.AppendLine($"OS:          {Environment.OSVersion}");
                sb.AppendLine($"Machine:     {Environment.MachineName}");
                sb.AppendLine($"CPU cores:   {Environment.ProcessorCount}");
                sb.AppendLine($"RAM (GC):    {totalRam:F1} GB available");
                sb.AppendLine($"Uptime:      {(int)uptime.TotalHours}h {uptime.Minutes}m");
                sb.AppendLine("Drives:");
                foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
                {
                    var total = drive.TotalSize / (1024.0 * 1024 * 1024);
                    var avail = drive.AvailableFreeSpace / (1024.0 * 1024 * 1024);
                    sb.AppendLine($"  {drive.Name} ({drive.DriveType})  {avail:F1} GB free / {total:F1} GB total");
                }

                return new ToolExecutionResult { TextResult = sb.ToString().TrimEnd() };
            }
        catch
            {
            return new ToolExecutionResult { TextResult = "Error getting system info." };
            }
        }

        public static ToolExecutionResult ExecuteGetRunningProcesses(ToolCallRequest request)
        {
            try
            {
                int topN = 20;
                if (request.Parameters.TryGetProperty("top_n", out var nEl) &&
                    nEl.ValueKind == System.Text.Json.JsonValueKind.Number)
                    topN = Math.Clamp(nEl.GetInt32(), 1, 200);

                var filter = GetString(request, "filter");
                var sorted = Process.GetProcesses()
                    .Where(p => string.IsNullOrEmpty(filter) ||
                                p.ProcessName.Contains(filter, StringComparison.OrdinalIgnoreCase))
                    .Select(p =>
                    {
                        try { return (p.ProcessName, MemMB: p.WorkingSet64 / (1024.0 * 1024), p.Id); }
                        catch { return (p.ProcessName, MemMB: 0.0, p.Id); }
                    })
                    .OrderByDescending(x => x.MemMB)
                    .Take(topN)
                    .ToList();

                var sb = new StringBuilder();
                sb.AppendLine($"{"Process",-30} {"PID",7} {"Memory (MB)",12}");
                sb.AppendLine(new string('-', 52));
                foreach (var (name, mem, pid) in sorted)
                    sb.AppendLine($"{name,-30} {pid,7} {mem,11:F1}");

                return new ToolExecutionResult { TextResult = sb.ToString().TrimEnd() };
            }
        catch
            {
            return new ToolExecutionResult { TextResult = "Error listing processes." };
            }
        }

        public static ToolExecutionResult ExecuteOpenFile(ToolCallRequest request)
        {
            var path = GetString(request, "path");
            if (string.IsNullOrWhiteSpace(path))
                return new ToolExecutionResult { TextResult = "Error: path parameter is required." };

            if (!File.Exists(path) && !Directory.Exists(path))
                return new ToolExecutionResult { TextResult = $"Error: Not found: {path}" };

            try
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                return new ToolExecutionResult { TextResult = $"Opened: {path}" };
            }
        catch
            {
            return new ToolExecutionResult { TextResult = "Error opening file." };
            }
        }

        public static ToolExecutionResult ExecuteGetActiveWindow()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return new ToolExecutionResult { TextResult = "Get active window is only supported on Windows." };

            try
            {
                var hwnd = GetForegroundWindow();
                var title = new StringBuilder(512);
                GetWindowText(hwnd, title, title.Capacity);
                GetWindowThreadProcessId(hwnd, out uint pid);

                string procName = string.Empty;
                try { procName = Process.GetProcessById((int)pid).ProcessName; } catch { }

                return new ToolExecutionResult
                {
                    TextResult = $"Active window: \"{title}\"\nProcess: {procName} (PID {pid})"
                };
            }
        catch
            {
            return new ToolExecutionResult { TextResult = "System operation failed." };
            }
        }

        public static Task<ToolExecutionResult> ExecuteGetSelectedTextAsync()
        {
            return Task.FromResult(new ToolExecutionResult
            {
                TextResult = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? "Get selected text is only supported on Windows."
                    : "Get selected text is only supported on Windows."
            });
        }

        public static void ShowSystemNotification(string title, string message)
        {
            Debug.WriteLine($"[Notification] {title}: {message}");
        }
    }
}
