using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Aire.Services
{
    /// <summary>
    /// Result of a command execution.
    /// </summary>
    public class CommandExecutionResult
    {
        public int ExitCode { get; set; }
        public string StandardOutput { get; set; } = string.Empty;
        public string StandardError { get; set; } = string.Empty;
        public long DurationMs { get; set; }
        public bool TimedOut { get; set; }
        public bool WasApproved { get; set; }

        public string CombinedOutput =>
            $"[Exit: {ExitCode}]{(TimedOut ? " (timeout)" : "")}\n" +
            $"{(string.IsNullOrEmpty(StandardOutput) ? "" : $"Output:\n{StandardOutput}\n")}" +
            $"{(string.IsNullOrEmpty(StandardError) ? "" : $"Error:\n{StandardError}")}";

        public string ToDisplayString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Command completed in {DurationMs}ms");
            if (TimedOut) sb.AppendLine("WARNING: Command timed out");
            sb.AppendLine($"Exit code: {ExitCode}");

            if (!string.IsNullOrEmpty(StandardOutput))
            {
                sb.AppendLine("--- Output ---");
                sb.AppendLine(StandardOutput);
            }

            if (!string.IsNullOrEmpty(StandardError))
            {
                sb.AppendLine("--- Error ---");
                sb.AppendLine(StandardError);
            }

            return sb.ToString();
        }
    }

    /// <summary>
    /// Cross-platform service for executing shell commands.
    /// On Windows uses cmd.exe /c; on Linux/macOS uses /bin/bash -c.
    /// </summary>
    public class CommandExecutionService
    {
        private const int DefaultTimeoutSeconds = 30;
        private const int MaxTimeoutSeconds = 300;
        private const int MaxOutputLength = 10000;

        /// <summary>
        /// Executes a shell command with the given parameters.
        /// </summary>
        public async Task<CommandExecutionResult> ExecuteAsync(
            string command,
            string? workingDirectory = null,
            int timeoutSeconds = DefaultTimeoutSeconds,
            string? shell = null)
        {
            if (string.IsNullOrWhiteSpace(command))
                throw new ArgumentException("Command cannot be empty", nameof(command));

            timeoutSeconds = Math.Clamp(timeoutSeconds, 1, MaxTimeoutSeconds);

            var resolvedWorkingDir = ResolveWorkingDirectory(workingDirectory);
            var (shellPath, shellArgs) = GetShellConfiguration(shell);

            var startInfo = new ProcessStartInfo
            {
                FileName               = shellPath,
                Arguments              = $"{shellArgs} {QuoteCommandArg(command, shellPath)}",
                WorkingDirectory       = resolvedWorkingDir,
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                RedirectStandardInput  = false,
                CreateNoWindow         = true,
                WindowStyle            = ProcessWindowStyle.Hidden
            };

            SetSafeEnvironmentVariables(startInfo);

            var stopwatch = Stopwatch.StartNew();
            var result    = new CommandExecutionResult();

            try
            {
                using var process = new Process { StartInfo = startInfo };
                var outputBuilder = new StringBuilder();
                var errorBuilder  = new StringBuilder();

                process.OutputDataReceived += (_, e) =>
                {
                    if (e.Data != null && outputBuilder.Length < MaxOutputLength)
                        outputBuilder.AppendLine(e.Data);
                };

                process.ErrorDataReceived += (_, e) =>
                {
                    if (e.Data != null && errorBuilder.Length < MaxOutputLength)
                        errorBuilder.AppendLine(e.Data);
                };

                if (!process.Start())
                    throw new InvalidOperationException($"Failed to start process: {shellPath}");

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                var completed = await Task.Run(() =>
                    process.WaitForExit(timeoutSeconds * 1000));

                stopwatch.Stop();

                if (!completed)
                {
                    try { process.Kill(entireProcessTree: true); } catch { }
                    result.TimedOut      = true;
                    result.StandardError = $"Command timed out after {timeoutSeconds} seconds";
                }
                else
                {
                    await Task.Delay(100); // let async readers flush
                    result.ExitCode = process.ExitCode;
                }

                result.StandardOutput = TruncateString(outputBuilder.ToString(), MaxOutputLength);
                if (!result.TimedOut)
                    result.StandardError = TruncateString(errorBuilder.ToString(), MaxOutputLength);
                result.DurationMs = stopwatch.ElapsedMilliseconds;
            }
            catch
            {
                stopwatch.Stop();
                result.ExitCode      = -1;
                result.StandardError = "Execution error: command failed.";
                result.DurationMs    = stopwatch.ElapsedMilliseconds;
            }

            return result;
        }

        /// <summary>
        /// Validates a command for dangerous patterns.
        /// Returns (IsSafe=false, Warning) if potentially dangerous.
        /// </summary>
        public (bool IsSafe, string Warning) ValidateCommandSafety(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
                return (false, "Command is empty");

            var dangerousPatterns = new[]
            {
                // Windows
                @"format\s+[A-Za-z]:",
                @"diskpart\s+.*select\s+disk",
                @"shutdown\s+.*\/s",
                @"del\s+.*\/s\s+.*\/q",
                @"rmdir\s+.*\/s\s+.*\/q",
                @"powershell\s+-EncodedCommand",
                // Linux/macOS
                @"rm\s+-rf\s+(\/|\.\.)",
                @"dd\s+.*if=.*of=",
                @":\s*\(\)\s*\{\s*:\s*\|\s*:\s*&\s*\}",
                @"mkfs\.",
                @"fdisk\s+",
                @"\|\s*bash\s*$",
                // Cross-platform
                @"curl\s+.*\|\s*(bash|sh|powershell)",
                @"wget\s+.*\|\s*(bash|sh|powershell)",
                // SQL
                @"DROP\s+TABLE",
                @"DROP\s+DATABASE",
                @"TRUNCATE\s+TABLE",
            };

            foreach (var pattern in dangerousPatterns)
            {
                if (Regex.IsMatch(command, pattern, RegexOptions.IgnoreCase))
                    return (false, $"Command matches dangerous pattern: {pattern}");
            }

            if (command.Contains("&&") && command.Contains("rm") && command.Contains("-rf"))
                return (false, "Suspicious command chaining with rm -rf");

            if (command.Contains(";") && command.Contains("chmod") && command.Contains("777"))
                return (false, "Suspicious command chaining with chmod 777");

            return (true, string.Empty);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static string ResolveWorkingDirectory(string? workingDirectory)
        {
            if (string.IsNullOrWhiteSpace(workingDirectory))
                return Environment.CurrentDirectory;

            if (workingDirectory.StartsWith("~"))
            {
                var home     = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var relative = workingDirectory.Substring(1).TrimStart('/', '\\');
                return Path.Combine(home, relative);
            }

            try { return Path.GetFullPath(workingDirectory); }
            catch { return Environment.CurrentDirectory; }
        }

        private static (string ShellPath, string ShellArgs) GetShellConfiguration(string? preferredShell)
        {
            if (!string.IsNullOrEmpty(preferredShell) && preferredShell != "auto")
            {
                return preferredShell.ToLowerInvariant() switch
                {
                    "cmd"        => ("cmd.exe",        "/c"),
                    "powershell" => ("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -Command"),
                    "bash"       => ("/bin/bash",       "-c"),
                    "sh"         => ("/bin/sh",          "-c"),
                    _            => GetDefaultShell()
                };
            }
            return GetDefaultShell();
        }

        private static (string ShellPath, string ShellArgs) GetDefaultShell()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return ("cmd.exe", "/c");

            // Linux / macOS
            if (File.Exists("/bin/bash"))
                return ("/bin/bash", "-c");
            return ("/bin/sh", "-c");
        }

        /// <summary>
        /// Wraps the command string so it is passed as a single argument to the shell.
        /// On Windows cmd.exe the whole command is passed as-is (cmd /c already handles it).
        /// On bash/sh we wrap in single quotes, escaping any embedded single quotes.
        /// </summary>
        private static string QuoteCommandArg(string command, string shellPath)
        {
            var shell = Path.GetFileName(shellPath).ToLowerInvariant();
            if (shell.StartsWith("cmd"))
                return command; // cmd /c takes everything that follows verbatim

            // bash / sh: wrap in single quotes, escape internal single quotes
            return "'" + command.Replace("'", "'\\''") + "'";
        }

        private static void SetSafeEnvironmentVariables(ProcessStartInfo startInfo)
        {
            startInfo.Environment.Clear();

            var sensitivePatterns = new[]
            {
                "PASSWORD", "SECRET", "KEY", "TOKEN", "API_",
                "AWS_", "AZURE_", "GCP_", "DATABASE_", "CONNECTION_STRING",
                "PRIVATE", "CREDENTIAL", "AUTH"
            };

            foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
            {
                var key = entry.Key?.ToString();
                if (key == null) continue;
                var value = entry.Value?.ToString() ?? string.Empty;

                bool sensitive = false;
                foreach (var p in sensitivePatterns)
                {
                    if (key.Contains(p, StringComparison.OrdinalIgnoreCase))
                    { sensitive = true; break; }
                }

                startInfo.EnvironmentVariables[key] = sensitive ? "***REDACTED***" : value;
            }

            startInfo.EnvironmentVariables["AIRE_COMMAND_EXECUTION"] = "true";
        }

        private static string TruncateString(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
                return text;
            return text.Substring(0, maxLength) +
                   $"\n...[Output truncated, {text.Length - maxLength} characters omitted]";
        }
    }
}
