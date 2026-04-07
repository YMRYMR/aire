using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Aire.Data;
using Aire.Domain.Providers;

namespace Aire.Providers
{
    /// <summary>
    /// Local Codex bridge provider.
    /// This talks to an official Codex CLI installation that the user has already authenticated,
    /// so Aire does not need to store or manage an OpenAI API key for this provider.
    /// </summary>
    public class CodexProvider : BaseAiProvider
    {
        private const int DefaultTimeoutMs = 120000;
        private const int ToolOnlySystemMessageLimit = 2500;
        private const int ToolOnlyConversationMessageLimit = 900;
        private const int NormalSystemMessageLimit = 6000;
        private const int NormalConversationMessageLimit = 2000;

        public override string ProviderType => "Codex";
        public override string DisplayName => "Codex (Local CLI bridge)";

        protected override ProviderCapabilities GetBaseCapabilities() =>
            ProviderCapabilities.TextChat |
            ProviderCapabilities.SystemPrompt |
            ProviderCapabilities.ToolCalling;

        public override ProviderFieldHints FieldHints => new()
        {
            ShowApiKey = false,
            ApiKeyRequired = false,
            ShowBaseUrl = false,
        };

        public override IReadOnlyList<ProviderAction> Actions => new[]
        {
            new ProviderAction
            {
                Id = "codex-install",
                Label = "Install Codex CLI",
                Placement = ProviderActionPlacement.ApiKeyArea,
            },
        };

        public override async Task<AiResponse> SendChatAsync(
            IEnumerable<ChatMessage> messages,
            CancellationToken cancellationToken = default)
        {
            var startTime = DateTime.UtcNow;

            try
            {
                var promptFile = Path.Combine(Path.GetTempPath(), $"aire-codex-{Guid.NewGuid():N}.txt");
                try
                {
                    await File.WriteAllTextAsync(promptFile, BuildPrompt(messages), cancellationToken).ConfigureAwait(false);

                    var result = await RunCodexAsync(promptFile, cancellationToken).ConfigureAwait(false);
                    if (result.ExitCode != 0)
                    {
                        return new AiResponse
                        {
                            IsSuccess = false,
                            ErrorMessage = string.IsNullOrWhiteSpace(result.ErrorOutput)
                                ? $"Codex exited with code {result.ExitCode}."
                                : result.ErrorOutput.Trim(),
                            Duration = DateTime.UtcNow - startTime
                        };
                    }

                    var output = result.Output.Trim();
                    if (string.IsNullOrWhiteSpace(output))
                    {
                        return new AiResponse
                        {
                            IsSuccess = false,
                            ErrorMessage = "Codex returned an empty response.",
                            Duration = DateTime.UtcNow - startTime
                        };
                    }

                    return new AiResponse
                    {
                        Content = output,
                        Duration = DateTime.UtcNow - startTime,
                        IsSuccess = true
                    };
                }
                finally
                {
                    try
                    {
                        if (File.Exists(promptFile))
                            File.Delete(promptFile);
                    }
                    catch
                    {
                    }
                }
            }
            catch (OperationCanceledException)
            {
                return new AiResponse
                {
                    IsSuccess = false,
                    ErrorMessage = "Codex request was cancelled.",
                    Duration = DateTime.UtcNow - startTime
                };
            }
            catch (Exception ex)
            {
            System.Diagnostics.Debug.WriteLine($"[WARN] [{GetType().Name}.SendChat] {ex.GetType().Name}");
                return new AiResponse
                {
                    IsSuccess = false,
                    ErrorMessage = "Codex request failed.",
                    Duration = DateTime.UtcNow - startTime
                };
            }
        }

        public override async Task<ProviderValidationResult> ValidateConfigurationAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var promptFile = Path.Combine(Path.GetTempPath(), $"aire-codex-validate-{Guid.NewGuid():N}.txt");
                try
                {
                    await File.WriteAllTextAsync(
                        promptFile,
                        "Reply with exactly this text and nothing else: OK",
                        cancellationToken).ConfigureAwait(false);

                    var result = await RunCodexAsync(promptFile, cancellationToken).ConfigureAwait(false);
                    if (result.ExitCode != 0)
                        return ProviderValidationResult.Fail($"Codex exited with code {result.ExitCode}.");

                    return IsValidationSuccessOutput(result.Output)
                        ? ProviderValidationResult.Ok()
                        : ProviderValidationResult.Fail("Codex responded but output was unexpected.");
                }
                finally
                {
                    try
                    {
                        if (File.Exists(promptFile))
                            File.Delete(promptFile);
                    }
                    catch
                    {
                    }
                }
            }
            catch (Exception ex)
            {
            System.Diagnostics.Debug.WriteLine($"[WARN] [{GetType().Name}.ValidateConfiguration] {ex.GetType().Name}");
                return ProviderValidationResult.Fail("Codex validation failed.");
            }
        }

        internal static string BuildPrompt(IEnumerable<ChatMessage> messages)
        {
            var messageList = messages?.ToList() ?? new List<ChatMessage>();
            var systemMessages = messageList
                .Where(message => string.Equals(message.Role, "system", StringComparison.OrdinalIgnoreCase))
                .ToList();
            var conversationMessages = messageList
                .Where(message => !string.Equals(message.Role, "system", StringComparison.OrdinalIgnoreCase))
                .ToList();
            var latestUserMessage = conversationMessages
                .LastOrDefault(message => string.Equals(message.Role, "user", StringComparison.OrdinalIgnoreCase))
                ?.Content;
            var isRetryFollowUp = IsRetryFollowUp(latestUserMessage);
            var requiresToolOnlyResponse = RequiresForcedToolOnlyResponse(latestUserMessage);
            var systemMessageLimit = requiresToolOnlyResponse ? ToolOnlySystemMessageLimit : NormalSystemMessageLimit;
            var conversationMessageLimit = requiresToolOnlyResponse ? ToolOnlyConversationMessageLimit : NormalConversationMessageLimit;
            var conversationWindow = SelectConversationWindow(conversationMessages, requiresToolOnlyResponse, isRetryFollowUp);

            var sb = new StringBuilder();
            sb.AppendLine("You are Codex, answering inside Aire.");
            sb.AppendLine("SYSTEM blocks below are higher priority than USER or ASSISTANT blocks. Obey them exactly.");
            sb.AppendLine("Do not mention internal CLI details unless the user explicitly asks.");
            sb.AppendLine("Never use Codex CLI's own file, search, shell, or browser actions when Aire tools are available.");
            sb.AppendLine("Never answer a file, system, browser, or search request from memory when Aire tools are available.");
            sb.AppendLine("When a task requires an Aire tool, your entire final answer must be exactly one Aire <tool_call> block.");
            if (isRetryFollowUp)
                sb.AppendLine("If the latest user message is a retry request like 'try again', infer they mean the most recent unfinished user task from the conversation context.");
            sb.AppendLine();

            if (systemMessages.Count > 0)
            {
                sb.AppendLine("SYSTEM INSTRUCTIONS (highest priority):");
                sb.AppendLine();

                foreach (var message in systemMessages.TakeLast(2))
                {
                    sb.AppendLine("<system_instruction>");
                    sb.AppendLine(TrimForPrompt(message.Content, systemMessageLimit));
                    if (!string.IsNullOrWhiteSpace(message.ImagePath))
                        sb.AppendLine($"[Attached image path: {message.ImagePath}]");
                    sb.AppendLine("</system_instruction>");
                    sb.AppendLine();
                }
            }

            sb.AppendLine("CONVERSATION:");
            sb.AppendLine();

            foreach (var message in conversationWindow)
            {
                var role = string.IsNullOrWhiteSpace(message.Role) ? "user" : message.Role;
                sb.Append(role.ToUpperInvariant());
                sb.AppendLine(":");
                sb.AppendLine(TrimForPrompt(message.Content, conversationMessageLimit));
                if (!string.IsNullOrWhiteSpace(message.ImagePath))
                    sb.AppendLine($"[Attached image path: {message.ImagePath}]");
                sb.AppendLine();
            }

            if (requiresToolOnlyResponse)
            {
                sb.AppendLine("FINAL RESPONSE RULE FOR THIS TURN:");
                sb.AppendLine("The latest user request clearly requires an Aire tool.");
                sb.AppendLine("Respond with ONLY one <tool_call>{...}</tool_call> block.");
                sb.AppendLine("Do not add prose, explanation, markdown, code fences, or multiple tool calls.");
            }
            else
            {
                sb.AppendLine("Respond to the latest user request.");
            }

            return sb.ToString();
        }

        internal static IReadOnlyList<ChatMessage> SelectConversationWindow(
            IReadOnlyList<ChatMessage> conversationMessages,
            bool requiresToolOnlyResponse,
            bool isRetryFollowUp)
        {
            if (requiresToolOnlyResponse)
                return SelectToolFocusedConversationWindow(conversationMessages, isRetryFollowUp);

            if (isRetryFollowUp)
                return SelectRetryConversationWindow(conversationMessages);

            return conversationMessages.TakeLast(8).ToList();
        }

        internal static IReadOnlyList<ChatMessage> SelectToolFocusedConversationWindow(IReadOnlyList<ChatMessage> conversationMessages, bool isRetryFollowUp = false)
        {
            if (conversationMessages.Count <= 4)
                return conversationMessages.ToList();

            var selected = new List<ChatMessage>();
            var latestUserIndex = -1;
            var previousUserIndex = -1;

            for (var i = conversationMessages.Count - 1; i >= 0; i--)
            {
                if (string.Equals(conversationMessages[i].Role, "user", StringComparison.OrdinalIgnoreCase))
                {
                    if (latestUserIndex < 0)
                    {
                        latestUserIndex = i;
                    }
                    else
                    {
                        previousUserIndex = i;
                        break;
                    }
                }
            }

            if (latestUserIndex > 0)
            {
                var immediateContextStart = Math.Max(0, latestUserIndex - 2);
                for (var i = immediateContextStart; i <= latestUserIndex; i++)
                    selected.Add(conversationMessages[i]);
            }

            if (isRetryFollowUp && previousUserIndex >= 0)
            {
                var previousContextStart = Math.Max(0, previousUserIndex - 1);
                for (var i = previousContextStart; i <= previousUserIndex; i++)
                {
                    if (!selected.Contains(conversationMessages[i]))
                        selected.Add(conversationMessages[i]);
                }
            }

            foreach (var message in conversationMessages.TakeLast(3))
            {
                if (!selected.Contains(message))
                    selected.Add(message);
            }

            return selected;
        }

        internal static IReadOnlyList<ChatMessage> SelectRetryConversationWindow(IReadOnlyList<ChatMessage> conversationMessages)
        {
            if (conversationMessages.Count <= 8)
                return conversationMessages.ToList();

            var selected = new List<ChatMessage>();
            var userMessages = conversationMessages
                .Select((message, index) => new { message, index })
                .Where(x => string.Equals(x.message.Role, "user", StringComparison.OrdinalIgnoreCase))
                .TakeLast(2)
                .ToList();

            foreach (var userMessage in userMessages)
            {
                var start = Math.Max(0, userMessage.index - 1);
                for (var i = start; i <= userMessage.index; i++)
                {
                    if (!selected.Contains(conversationMessages[i]))
                        selected.Add(conversationMessages[i]);
                }
            }

            foreach (var message in conversationMessages.TakeLast(4))
            {
                if (!selected.Contains(message))
                    selected.Add(message);
            }

            return selected;
        }

        internal static bool RequiresForcedToolOnlyResponse(string? content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return false;

            var normalized = content.ToLowerInvariant();
            string[] triggers =
            [
                "using a tool",
                "use a tool",
                "list the files",
                "list files",
                "read file",
                "open file",
                "search files",
                "find files",
                "search the repository",
                "search the repo",
                "directory",
                "folder",
                "run command",
                "execute command",
                "open browser",
                "open the page",
                "navigate to",
                "take screenshot",
                "click",
                "type ",
                "write to file",
                "edit file",
                "delete file",
                "move file",
            ];

            return triggers.Any(normalized.Contains);
        }

        internal static bool IsRetryFollowUp(string? content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return false;

            var normalized = content.Trim().ToLowerInvariant();
            string[] retryPhrases =
            [
                "try again",
                "retry",
                "again",
                "do it again",
                "continue",
                "please continue",
                "try that again"
            ];

            return retryPhrases.Contains(normalized, StringComparer.Ordinal);
        }

        internal static bool IsValidationSuccessOutput(string? output)
            => string.Equals(output?.Trim(), "OK", StringComparison.OrdinalIgnoreCase);

        private static string TrimForPrompt(string? content, int maxChars)
        {
            if (string.IsNullOrEmpty(content) || content.Length <= maxChars)
                return content ?? string.Empty;

            return content.Substring(0, maxChars) + "\n[Truncated for Codex CLI prompt size]";
        }

        private async Task<(int ExitCode, string Output, string ErrorOutput)> RunCodexAsync(
            string promptFile,
            CancellationToken cancellationToken)
        {
            var status = GetCliStatus();
            var codexCli = status.CliPath;
            if (codexCli == null)
            {
                return (-1, string.Empty, status.UserMessage);
            }

            var outputFile = Path.Combine(Path.GetTempPath(), $"aire-codex-output-{Guid.NewGuid():N}.txt");
            var promptContent = await File.ReadAllTextAsync(promptFile, cancellationToken).ConfigureAwait(false);
            var psi = CreateCodexProcessStartInfo(codexCli, outputFile);

            try
            {
                using var process = new Process { StartInfo = psi };
                if (!process.Start())
                    throw new InvalidOperationException("Failed to start Codex CLI.");

                var inputTask = process.StandardInput.WriteAsync(promptContent.AsMemory(), cancellationToken);
                await inputTask.ConfigureAwait(false);
                await process.StandardInput.FlushAsync().ConfigureAwait(false);
                process.StandardInput.Close();

                using var _ = cancellationToken.Register(() =>
                {
                    try
                    {
                        if (!process.HasExited)
                            process.Kill(entireProcessTree: true);
                    }
                    catch
                    {
                    }
                });

                var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
                var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
                var waitTask = process.WaitForExitAsync(cancellationToken);

                var timeoutTask = Task.Delay(DefaultTimeoutMs, cancellationToken);
                var completed = await Task.WhenAny(Task.WhenAll(stdoutTask, stderrTask, waitTask), timeoutTask)
                    .ConfigureAwait(false);

                if (completed == timeoutTask)
                {
                    try
                    {
                        if (!process.HasExited)
                            process.Kill(entireProcessTree: true);
                    }
                    catch
                    {
                    }

                    throw new TimeoutException("Codex CLI timed out.");
                }

                var stdout = await stdoutTask.ConfigureAwait(false);
                var stderr = await stderrTask.ConfigureAwait(false);
                var output = string.Empty;
                if (File.Exists(outputFile))
                {
                    output = (await File.ReadAllTextAsync(outputFile, cancellationToken).ConfigureAwait(false)).Trim();
                }

                if (string.IsNullOrWhiteSpace(output))
                    output = stdout.Trim();

                return (process.ExitCode, output, stderr);
            }
            finally
            {
                try
                {
                    if (File.Exists(outputFile))
                        File.Delete(outputFile);
                }
                catch
                {
                }
            }
        }

        public static CodexCliStatus GetCliStatus()
        {
            var resolution = FindCodexCli();
            if (resolution.Path != null)
            {
                return new CodexCliStatus(
                    true,
                    resolution.Path,
                    resolution.SawStorePackagePath,
                    $"Codex CLI detected at {resolution.Path}");
            }

            var message = resolution.SawStorePackagePath
                ? "Codex Windows app was detected, but its packaged executable is not directly launchable from Aire.\n" +
                  "Install the standalone Codex CLI with:\n  npm install -g @openai/codex\n" +
                  "or make sure a working 'codex' command is available in a normal terminal, then restart Aire."
                : "Codex CLI not found. Install it with:\n  npm install -g @openai/codex\nThen restart Aire.";

            return new CodexCliStatus(false, null, resolution.SawStorePackagePath, message);
        }

        public static bool HasLaunchableCli() => GetCliStatus().IsInstalled;

        private ProcessStartInfo CreateCodexProcessStartInfo(string codexCli, string outputFile)
        {
            var model = string.IsNullOrWhiteSpace(Config.Model) ? "default" : Config.Model.Trim();
            var arguments = new List<string>
            {
                "exec",
                "--color", "never",
                "--ephemeral",
                "--sandbox", "read-only",
                "-c", "shell_environment_policy.inherit=none",
                "--output-last-message", outputFile
            };

            if (!string.Equals(model, "default", StringComparison.OrdinalIgnoreCase))
            {
                arguments.Add("--model");
                arguments.Add(model);
            }

            arguments.Add("-");

            var psi = new ProcessStartInfo
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = GetWorkingDirectory(),
                StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                StandardOutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                StandardErrorEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
            };

            if (codexCli.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) ||
                codexCli.EndsWith(".bat", StringComparison.OrdinalIgnoreCase))
            {
                psi.FileName = "cmd.exe";
                psi.ArgumentList.Add("/d");
                psi.ArgumentList.Add("/c");
                psi.ArgumentList.Add(codexCli);
            }
            else
            {
                psi.FileName = codexCli;
            }

            foreach (var argument in arguments)
                psi.ArgumentList.Add(argument);

            return psi;
        }

        /// <summary>
        /// Locates the codex CLI executable. Checks common npm global bin locations
        /// because tray apps often launch with a minimal PATH that excludes npm.
        /// </summary>
        private static (string? Path, bool SawStorePackagePath) FindCodexCli()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            var preferredCandidates = new[]
            {
                // Microsoft Store apps sometimes expose launchable stubs here.
                Path.Combine(localApp, "Microsoft", "WindowsApps", "codex.exe"),
                Path.Combine(localApp, "Microsoft", "WindowsApps", "codex.cmd"),
                // Native binary shipped inside the standalone npm package.
                Path.Combine(appData, "npm", "node_modules", "@openai", "codex", "node_modules", "@openai", "codex-win32-x64", "vendor", "x86_64-pc-windows-msvc", "codex", "codex.exe"),
                Path.Combine(appData, "npm", "node_modules", "@openai", "codex", "vendor", "x86_64-pc-windows-msvc", "codex", "codex.exe"),
                // npm global (default Windows location)
                Path.Combine(appData, "npm", "codex.cmd"),
                Path.Combine(appData, "npm", "codex"),
                // npm with custom global prefix in home dir
                Path.Combine(userProfile, ".npm-global", "bin", "codex.cmd"),
                Path.Combine(userProfile, ".npm-global", "bin", "codex"),
                // Volta
                Path.Combine(localApp, "Volta", "bin", "codex.cmd"),
                Path.Combine(localApp, "Volta", "bin", "codex"),
                // fnm (Fast Node Manager)
                Path.Combine(localApp, "fnm_multishells", "codex.cmd"),
                // Node.js installed system-wide
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs", "codex.cmd"),
            };

            var whereResults = GetWhereResults();
            var selection = SelectCodexCliPath(preferredCandidates, whereResults, File.Exists);
            if (selection.Path != null)
                return selection;

            // nvm for Windows — any version under %APPDATA%\nvm\
            var nvmDir = Path.Combine(appData, "nvm");
            if (Directory.Exists(nvmDir))
            {
                var match = Directory
                    .GetFiles(nvmDir, "codex.cmd", SearchOption.AllDirectories)
                    .FirstOrDefault();
                if (match != null)
                    return (match, selection.SawStorePackagePath);
            }

            var packagedBinary = FindStandalonePackageBinary(appData);
            if (packagedBinary != null)
                return (packagedBinary, selection.SawStorePackagePath);

            return selection;
        }

        private static string? FindStandalonePackageBinary(string appData)
        {
            try
            {
                var packageRoot = Path.Combine(appData, "npm", "node_modules", "@openai", "codex");
                if (!Directory.Exists(packageRoot))
                    return null;

                return Directory
                    .EnumerateFiles(packageRoot, "codex.exe", SearchOption.AllDirectories)
                    .FirstOrDefault(path =>
                        path.IndexOf(@"\vendor\", StringComparison.OrdinalIgnoreCase) >= 0 &&
                        path.IndexOf(@"\codex\", StringComparison.OrdinalIgnoreCase) >= 0 &&
                        path.IndexOf(@"\WindowsApps\", StringComparison.OrdinalIgnoreCase) < 0);
            }
            catch
            {
                return null;
            }
        }

        private static IEnumerable<string> GetWhereResults()
        {
            try
            {
                var wherePsi = new ProcessStartInfo("where.exe", "codex")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true
                };
                using var wp = Process.Start(wherePsi);
                if (wp != null)
                {
                    var results = new List<string>();
                    string? line;
                    while ((line = wp.StandardOutput.ReadLine()?.Trim()) != null)
                    {
                        if (!string.IsNullOrEmpty(line))
                            results.Add(line);
                    }
                    wp.WaitForExit(3000);
                    return results;
                }
            }
            catch { }

            return Array.Empty<string>();
        }

        internal static (string? Path, bool SawStorePackagePath) SelectCodexCliPath(
            IEnumerable<string> preferredCandidates,
            IEnumerable<string> whereResults,
            Func<string, bool> fileExists)
        {
            bool sawStorePackagePath = false;

            foreach (var candidate in preferredCandidates.Where(path => !string.IsNullOrWhiteSpace(path)))
            {
                if (fileExists(candidate))
                    return (candidate, sawStorePackagePath);
            }

            foreach (var path in whereResults.Where(path => !string.IsNullOrWhiteSpace(path)))
            {
                if (IsStorePackageBinary(path))
                {
                    sawStorePackagePath = true;
                    continue;
                }

                if (fileExists(path))
                    return (path, sawStorePackagePath);
            }

            return (null, sawStorePackagePath);
        }

        private static bool IsStorePackageBinary(string path)
            => path.IndexOf(@"\Program Files\WindowsApps\", StringComparison.OrdinalIgnoreCase) >= 0
               && path.IndexOf(@"\Microsoft\WindowsApps\", StringComparison.OrdinalIgnoreCase) < 0;

        private static string GetWorkingDirectory()
        {
            try
            {
                var current = Environment.CurrentDirectory;
                if (!string.IsNullOrWhiteSpace(current) && Directory.Exists(current))
                    return current;
            }
            catch
            {
            }

            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }
    }
}
