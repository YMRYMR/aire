using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Aire.Services
{
    /// <summary>
    /// Discovers and launches installed applications across Windows, Linux, and macOS.
    /// Results are cached in memory for the lifetime of the process.
    /// </summary>
    public class ApplicationLauncherService
    {
        internal static readonly Dictionary<string, string> _resolvedCache
            = new(StringComparer.OrdinalIgnoreCase);

        private static readonly string[] WindowsCommonPaths =
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs"),
            @"C:\Program Files",
            @"C:\Program Files (x86)",
        };

        private static readonly string[] LinuxCommonPaths =
        {
            "/usr/bin", "/usr/local/bin", "/snap/bin", "/opt",
            "/usr/share/applications",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "bin"),
        };

        private static readonly string[] MacCommonPaths =
        {
            "/Applications", "/usr/local/bin", "/opt/homebrew/bin",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Applications"),
        };

        // ── FindApplication ───────────────────────────────────────────────────

        public string? FindApplication(string appName)
        {
            if (string.IsNullOrWhiteSpace(appName)) return null;
            if (_resolvedCache.TryGetValue(appName, out var cached)) return cached;

            string? result;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                result = FindApplicationWindows(appName);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                result = FindApplicationLinux(appName);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                result = FindApplicationMac(appName);
            else
                result = null;

            if (result != null) _resolvedCache[appName] = result;
            return result;
        }

        private string? FindApplicationWindows(string appName)
        {
            var exeName = appName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? appName : appName + ".exe";
            var baseName = Path.GetFileNameWithoutExtension(exeName);
            var dirKey   = Regex.Replace(baseName, @"[-_.]\d.*$", "");
            if (string.IsNullOrEmpty(dirKey)) dirKey = baseName;

            // 1. PATH
            foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
            {
                if (string.IsNullOrEmpty(dir)) continue;
                var full = Path.Combine(dir, exeName);
                if (File.Exists(full)) return full;
            }

            // 2. Registry App Paths (Windows only)
            if (OperatingSystem.IsWindows())
            {
                var reg = FindInRegistryAppPaths(appName);
                if (reg != null) return reg;
            }

            // 3. Smart directory scan
            foreach (var baseDir in WindowsCommonPaths)
            {
                if (!Directory.Exists(baseDir)) continue;

                var direct = Path.Combine(baseDir, exeName);
                if (File.Exists(direct)) return direct;

                IEnumerable<string> subs;
                try { subs = Directory.EnumerateDirectories(baseDir); } catch { continue; }
                foreach (var sub in subs)
                {
                    if (!Path.GetFileName(sub).Contains(dirKey, StringComparison.OrdinalIgnoreCase))
                        continue;
                    var found = SearchDirRobust(sub, exeName) ?? SearchDirRobust(sub, dirKey + "*.exe");
                    if (found != null) return found;
                }
            }

            return null;
        }

        private string? FindApplicationLinux(string appName)
        {
            foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
            {
                if (string.IsNullOrEmpty(dir)) continue;
                var full = Path.Combine(dir, appName);
                if (File.Exists(full) && IsExecutable(full)) return full;
            }

            foreach (var baseDir in LinuxCommonPaths)
            {
                if (!Directory.Exists(baseDir)) continue;
                try
                {
                    var match = Directory.EnumerateFiles(baseDir, appName, SearchOption.AllDirectories)
                        .FirstOrDefault(IsExecutable);
                    if (match != null) return match;
                }
                catch { }
            }

            return FindViaDesktopFile(appName);
        }

        private string? FindApplicationMac(string appName)
        {
            foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
            {
                if (string.IsNullOrEmpty(dir)) continue;
                var full = Path.Combine(dir, appName);
                if (File.Exists(full) && IsExecutable(full)) return full;
            }

            foreach (var baseDir in MacCommonPaths)
            {
                if (!Directory.Exists(baseDir)) continue;
                try
                {
                    foreach (var bundle in Directory.EnumerateDirectories(baseDir, "*.app", SearchOption.AllDirectories))
                    {
                        var bundleName = Path.GetFileNameWithoutExtension(bundle);
                        if (!bundleName.Contains(appName, StringComparison.OrdinalIgnoreCase)) continue;

                        var exePath = Path.Combine(bundle, "Contents", "MacOS", bundleName);
                        if (File.Exists(exePath)) return exePath;

                        var macosDir = Path.Combine(bundle, "Contents", "MacOS");
                        if (Directory.Exists(macosDir))
                        {
                            var exe = Directory.GetFiles(macosDir).FirstOrDefault();
                            if (exe != null) return exe;
                        }
                    }
                }
                catch { }
            }

            return null;
        }

        // ── LaunchApplication ─────────────────────────────────────────────────

        public int? LaunchApplication(string appName, string? arguments = null, string? workingDirectory = null)
        {
            try
            {
                var execPath = File.Exists(appName) && Path.IsPathRooted(appName)
                    ? appName : FindApplication(appName);
                if (execPath == null) return null;

                var proc = Process.Start(new ProcessStartInfo
                {
                    FileName         = execPath,
                    Arguments        = arguments ?? string.Empty,
                    WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
                    UseShellExecute  = true,
                });
                return proc?.Id;
            }
            catch { return null; }
        }

        public int? OpenFileWithDefaultApplication(string filePath)
        {
            if (!File.Exists(filePath) && !Directory.Exists(filePath)) return null;
            try
            {
                var proc = Process.Start(new ProcessStartInfo { FileName = filePath, UseShellExecute = true });
                return proc?.Id;
            }
            catch { return null; }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        internal static string? SearchDirRobust(string root, string pattern)
        {
            try
            {
                var match = Directory.EnumerateFiles(root, pattern).FirstOrDefault();
                if (match != null) return match;
            }
            catch { }

            IEnumerable<string> subs;
            try { subs = Directory.EnumerateDirectories(root); } catch { return null; }
            foreach (var sub in subs)
            {
                var found = SearchDirRobust(sub, pattern);
                if (found != null) return found;
            }
            return null;
        }

        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private static string? FindInRegistryAppPaths(string appName)
        {
            const string regPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths";
            foreach (var keyName in new[] { appName + ".exe", appName })
            {
                foreach (var hive in new[] { Microsoft.Win32.Registry.LocalMachine, Microsoft.Win32.Registry.CurrentUser })
                {
                    using var key = hive.OpenSubKey(Path.Combine(regPath, keyName));
                    var path = key?.GetValue("")?.ToString();
                    if (!string.IsNullOrEmpty(path) && File.Exists(path))
                        return path;
                }
            }
            return null;
        }

        internal static bool IsExecutable(string path)
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var ext = Path.GetExtension(path).ToLowerInvariant();
                    return ext is ".exe" or ".com" or ".bat" or ".cmd";
                }
                var mode = File.GetUnixFileMode(path);
                return (mode & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0;
            }
            catch { return false; }
        }

        private string? FindViaDesktopFile(string appName)
        {
            string[] desktopDirs =
            {
                "/usr/share/applications", "/usr/local/share/applications",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "applications"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local/share/applications"),
            };

            foreach (var dir in desktopDirs)
            {
                if (!Directory.Exists(dir)) continue;
                try
                {
                    foreach (var file in Directory.EnumerateFiles(dir, "*.desktop", SearchOption.AllDirectories))
                    {
                        if (!Path.GetFileNameWithoutExtension(file).Contains(appName, StringComparison.OrdinalIgnoreCase))
                            continue;
                        var exec = ParseDesktopExec(file);
                        if (exec != null) return exec;
                    }
                }
                catch { }
            }
            return null;
        }

        internal string? ParseDesktopExec(string desktopFile)
        {
            try
            {
                bool inEntry = false;
                foreach (var line in File.ReadAllLines(desktopFile))
                {
                    var t = line.Trim();
                    if (t == "[Desktop Entry]") { inEntry = true; continue; }
                    if (inEntry && t.StartsWith('[')) break;
                    if (!inEntry || !t.StartsWith("Exec=", StringComparison.OrdinalIgnoreCase)) continue;

                    var exec = Regex.Replace(t[5..].Trim(), @"\s*%[a-zA-Z]\s*", " ");
                    var cmd  = exec.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                    if (cmd == null) continue;
                    if (Path.IsPathRooted(cmd) && File.Exists(cmd)) return cmd;
                    return FindInPathEnv(cmd);
                }
            }
            catch { }
            return null;
        }

        private static string? FindInPathEnv(string command)
        {
            foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
            {
                if (string.IsNullOrEmpty(dir)) continue;
                var full = Path.Combine(dir, command);
                if (File.Exists(full)) return full;
            }
            return null;
        }
    }
}
