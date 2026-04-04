using System;
using System.IO;
using System.Threading.Tasks;
using static Aire.Services.Tools.ToolHelpers;

namespace Aire.Services.Tools
{
    /// <summary>
    /// Handles command execution and best-effort application launch.
    /// </summary>
    public class CommandToolService
    {
        private readonly CommandExecutionService _commandService;
        private readonly ApplicationLauncherService _appLauncher = new();

        public CommandToolService(CommandExecutionService commandService)
        {
            _commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));
        }

        public async Task<ToolExecutionResult> ExecuteAsync(ToolCallRequest request)
        {
            try
            {
                var command          = GetString(request, "command");
                var workingDirectory = GetString(request, "working_directory");
                var timeoutStr       = GetString(request, "timeout_seconds");
                var shell            = GetString(request, "shell");

                if (string.IsNullOrWhiteSpace(command))
                    return new ToolExecutionResult { TextResult = "Error: Command parameter is required" };

                // Try to detect if this is a bare application launch request
                var appLaunchResult = TryLaunchApplication(command, workingDirectory);
                if (appLaunchResult != null)
                    return appLaunchResult;

                // Parse timeout (default 30 seconds)
                int timeout = 30;
                if (!string.IsNullOrEmpty(timeoutStr) && int.TryParse(timeoutStr, out int parsedTimeout))
                    timeout = Math.Clamp(parsedTimeout, 1, 300);

                var result = await _commandService.ExecuteAsync(
                    command,
                    string.IsNullOrEmpty(workingDirectory) ? null : workingDirectory,
                    timeout,
                    string.IsNullOrEmpty(shell) ? null : shell);

                return new ToolExecutionResult { TextResult = result.ToDisplayString() };
            }
        catch
            {
            return new ToolExecutionResult { TextResult = "Error executing command." };
            }
        }

        /// <summary>Gets a human-readable description of the execute_command request.</summary>
        public string GetDescription(ToolCallRequest request, CommandExecutionService commandService)
        {
            var command    = GetString(request, "command");
            var workingDir = GetString(request, "working_directory");

            if (string.IsNullOrEmpty(command))
                return "Execute command";

            var description = $"Execute command: {command}";

            if (!string.IsNullOrEmpty(workingDir))
                description += $"\nWorking directory: {workingDir}";

            var safetyCheck = commandService.ValidateCommandSafety(command);
            if (!safetyCheck.IsSafe)
                description += $"\nSafety warning: {safetyCheck.Warning}";

            return description;
        }

        /// <summary>
        /// Tries to launch a bare application name using ApplicationLauncherService
        /// (registry, PATH, and common install locations). Returns null if the command
        /// looks like a shell command rather than an app launch request.
        /// </summary>
        private ToolExecutionResult? TryLaunchApplication(string command, string? workingDirectory)
        {
            var trimmed = command.Trim();
            if (string.IsNullOrEmpty(trimmed))
                return null;

            // Skip obvious shell operators
            if (trimmed.Contains('|') || trimmed.Contains('&') || trimmed.Contains('>') || trimmed.Contains('<'))
                return null;

            var parts     = trimmed.Split(new[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return null;

            var appName   = parts[0];
            var arguments = parts.Length > 1 ? parts[1] : null;

            // Only attempt for bare names (no path separators)
            if (appName.Contains('/') || appName.Contains('\\'))
                return null;

            // Skip if it looks like a shell command flag (e.g. "echo -n")
            if (parts.Length > 1 && (arguments!.StartsWith("-") || arguments.StartsWith("/")))
                return null;

            // Only try for known extensions or no extension
            var ext = Path.GetExtension(appName).ToLowerInvariant();
            if (!string.IsNullOrEmpty(ext) && ext != ".exe" && ext != ".cmd" && ext != ".bat")
                return null;

            // Use ApplicationLauncherService for smart discovery (registry, PATH, Program Files)
            var resolvedPath = _appLauncher.FindApplication(appName);
            if (resolvedPath != null)
            {
                var pid = _appLauncher.LaunchApplication(appName, arguments, workingDirectory);
                if (pid.HasValue)
                {
                    var argDesc = string.IsNullOrEmpty(arguments) ? "with no arguments" : $"with argument(s): {arguments}";
                    return new ToolExecutionResult
                    {
                        TextResult = $"SUCCESS: '{appName}' opened {argDesc} (PID: {pid.Value}).\n" +
                                     $"Executable: \"{resolvedPath}\".\n" +
                                     $"Hint: If you need to interact with this application (e.g. type text), " +
                                     $"you should now call begin_keyboard_session (preferred) or begin_mouse_session."
                    };
                }
            }

            // Fallback: try shell-execute for the app name directly
            try
            {
                var psi  = new System.Diagnostics.ProcessStartInfo(appName, arguments ?? "")
                {
                    UseShellExecute  = true,
                    WorkingDirectory = string.IsNullOrEmpty(workingDirectory)
                        ? Environment.CurrentDirectory
                        : workingDirectory,
                };
                var proc = System.Diagnostics.Process.Start(psi);
                if (proc != null)
                {
                    var argDesc = string.IsNullOrEmpty(arguments) ? "with no arguments" : $"with argument(s): {arguments}";
                    return new ToolExecutionResult
                    {
                        TextResult = $"SUCCESS: '{appName}' opened {argDesc} (PID: {proc.Id}).\n" +
                                     $"Hint: If you need to interact with this application (e.g. type text), " +
                                     $"you should now call begin_keyboard_session (preferred) or begin_mouse_session."
                    };
                }
            }
            catch { /* Not found — fall through to shell execution */ }

            return null;
        }
    }
}
