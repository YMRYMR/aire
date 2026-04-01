using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Aire.Services.Tools.ToolHelpers;

namespace Aire.Services.Tools
{
    /// <summary>
    /// Handles system tools: clipboard, notify, system info, processes, active window,
    /// selected text, and open file.
    /// </summary>
    internal class SystemToolService
    {
        private static readonly HashSet<string> UnsafeOpenExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".exe", ".com", ".bat", ".cmd", ".ps1", ".psm1", ".vbs", ".vbe",
            ".js", ".jse", ".wsf", ".wsh", ".hta", ".msi", ".msp", ".msc",
            ".scr", ".cpl", ".lnk", ".url", ".reg"
        };

        // ── Win32 P/Invoke ───────────────────────────────────────────────────

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MEMORYSTATUSEX
        {
            public uint  dwLength;
            public uint  dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        // ── Public entry points ──────────────────────────────────────────────

        public static ToolExecutionResult ExecuteGetClipboard()
        {
            string text = string.Empty;
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                try { text = System.Windows.Clipboard.GetText(); }
                catch { text = string.Empty; }
            });
            return new ToolExecutionResult
            {
                TextResult = string.IsNullOrEmpty(text) ? "(Clipboard is empty or contains non-text data)" : text
            };
        }

        public static ToolExecutionResult ExecuteSetClipboard(ToolCallRequest request)
        {
            var text = GetString(request, "text");
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                try { System.Windows.Clipboard.SetText(text); }
                catch { /* ignore if clipboard is locked */ }
            });
            return new ToolExecutionResult { TextResult = $"Copied {text.Length} character(s) to clipboard." };
        }

        public static ToolExecutionResult ExecuteNotify(ToolCallRequest request)
        {
            var title   = GetString(request, "title");
            var message = GetString(request, "message");
            ShowSystemNotification(title, message);
            return new ToolExecutionResult { TextResult = $"Notification shown: \"{title}\"" };
        }

        public static ToolExecutionResult ExecuteGetSystemInfo()
        {
            try
            {
                var mem = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
                GlobalMemoryStatusEx(ref mem);

                var totalRam = mem.ullTotalPhys / (1024.0 * 1024 * 1024);
                var availRam = mem.ullAvailPhys / (1024.0 * 1024 * 1024);

                var uptimeMs  = Environment.TickCount64;
                var uptime    = TimeSpan.FromMilliseconds(uptimeMs);
                var uptimeStr = $"{(int)uptime.TotalHours}h {uptime.Minutes}m";

                var sb = new StringBuilder();
                sb.AppendLine($"OS:          {Environment.OSVersion}");
                sb.AppendLine($"Machine:     {Environment.MachineName}");
                sb.AppendLine($"CPU cores:   {Environment.ProcessorCount}");
                sb.AppendLine($"RAM total:   {totalRam:F1} GB");
                sb.AppendLine($"RAM avail:   {availRam:F1} GB ({100 - mem.dwMemoryLoad}% free)");
                sb.AppendLine($"Uptime:      {uptimeStr}");

                sb.AppendLine("Drives:");
                foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
                {
                    var total = drive.TotalSize          / (1024.0 * 1024 * 1024);
                    var avail = drive.AvailableFreeSpace / (1024.0 * 1024 * 1024);
                    sb.AppendLine($"  {drive.Name} ({drive.DriveType})  {avail:F1} GB free / {total:F1} GB total");
                }

                return new ToolExecutionResult { TextResult = sb.ToString().TrimEnd() };
            }
            catch (Exception ex)
            {
                return new ToolExecutionResult { TextResult = $"Error getting system info: {ex.Message}" };
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

                var procs = Process.GetProcesses()
                    .Where(p => string.IsNullOrEmpty(filter) ||
                                p.ProcessName.Contains(filter, StringComparison.OrdinalIgnoreCase));

                var sorted = procs
                    .Select(p =>
                    {
                        try   { return (p.ProcessName, MemMB: p.WorkingSet64 / (1024.0 * 1024), p.Id); }
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
            catch (Exception ex)
            {
                return new ToolExecutionResult { TextResult = $"Error listing processes: {ex.Message}" };
            }
        }

        public static ToolExecutionResult ExecuteGetActiveWindow()
        {
            try
            {
                var hwnd  = GetForegroundWindow();
                var title = new StringBuilder(512);
                GetWindowText(hwnd, title, title.Capacity);
                GetWindowThreadProcessId(hwnd, out uint pid);

                string procName = "";
                try { procName = Process.GetProcessById((int)pid).ProcessName; }
                catch { }

                return new ToolExecutionResult
                {
                    TextResult = $"Active window: \"{title}\"\nProcess: {procName} (PID {pid})"
                };
            }
            catch (Exception ex)
            {
                return new ToolExecutionResult { TextResult = $"Error: {ex.Message}" };
            }
        }

        public static async Task<ToolExecutionResult> ExecuteGetSelectedTextAsync()
        {
            try
            {
                string? savedText = null;
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    try { savedText = System.Windows.Clipboard.ContainsText()
                              ? System.Windows.Clipboard.GetText() : null; }
                    catch { }
                });

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    try { System.Windows.Clipboard.Clear(); } catch { }
                });

                await Task.Run(() =>
                {
                    Thread.Sleep(100);
                    System.Windows.Forms.SendKeys.SendWait("^c");
                    Thread.Sleep(300);
                });

                string selected = string.Empty;
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    try { selected = System.Windows.Clipboard.ContainsText()
                              ? System.Windows.Clipboard.GetText() : string.Empty; }
                    catch { }
                });

                if (savedText is not null)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        try { System.Windows.Clipboard.SetText(savedText); } catch { }
                    });
                }

                return new ToolExecutionResult
                {
                    TextResult = string.IsNullOrEmpty(selected)
                        ? "(Nothing selected or clipboard could not be read)"
                        : selected
                };
            }
            catch (Exception ex)
            {
                return new ToolExecutionResult { TextResult = $"Error: {ex.Message}" };
            }
        }

        public static ToolExecutionResult ExecuteOpenFile(ToolCallRequest request)
        {
            var path = GetString(request, "path");
            if (string.IsNullOrWhiteSpace(path))
                return new ToolExecutionResult { TextResult = "Error: path parameter is required." };

            if (!File.Exists(path) && !Directory.Exists(path))
                return new ToolExecutionResult { TextResult = $"Error: Not found: {path}" };

            if (IsPotentiallyExecutableTarget(path))
                return new ToolExecutionResult { TextResult = $"Error: refusing to shell-open potentially executable target: {path}" };

            try
            {
                if (Directory.Exists(path))
                {
                    var explorer = new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        UseShellExecute = false
                    };
                    explorer.ArgumentList.Add(path);
                    Process.Start(explorer);
                }
                else
                {
                    Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                }
                return new ToolExecutionResult { TextResult = $"Opened: {path}" };
            }
            catch (Exception ex)
            {
                return new ToolExecutionResult { TextResult = $"Error opening file: {ex.Message}" };
            }
        }

        // ── Shared notification helper ────────────────────────────────────────

        public static void ShowSystemNotification(string title, string message)
        {
            _ = Task.Run(() =>
            {
                try
                {
                    using var icon = new System.Windows.Forms.NotifyIcon();
                    icon.Icon    = System.Drawing.SystemIcons.Information;
                    icon.Visible = true;
                    icon.ShowBalloonTip(6000, title, message, System.Windows.Forms.ToolTipIcon.Info);
                    Thread.Sleep(7000);
                    icon.Visible = false;
                }
                catch { /* ignore if icon fails */ }
            });
        }

        internal static bool IsPotentiallyExecutableTarget(string path)
        {
            if (Directory.Exists(path))
                return false;

            var extension = Path.GetExtension(path);
            if (string.IsNullOrWhiteSpace(extension))
                return true;

            return UnsafeOpenExtensions.Contains(extension);
        }
    }
}
