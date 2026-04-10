using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Aire.Data;
using Aire.Domain.Providers;
using Aire.Services;

namespace Aire.Providers
{
    /// <summary>
    /// Local Claude Code bridge provider.
    /// This talks to a Claude Code CLI installation that the user has already authenticated,
    /// so Aire does not need to store or manage an API key for this provider.
    /// </summary>
    public class ClaudeCodeProvider : BaseAiProvider
    {
        private const int DefaultTimeoutMs = 120000;

        public override string ProviderType => "ClaudeCode";
        public override string DisplayName => "Claude Code";

        protected override ProviderCapabilities GetBaseCapabilities() =>
            ProviderCapabilities.TextChat |
            ProviderCapabilities.Streaming |
            ProviderCapabilities.ToolCalling |
            ProviderCapabilities.SystemPrompt;

        public override ProviderFieldHints FieldHints => new()
        {
            ShowApiKey = false,
            ApiKeyRequired = false,
            ShowBaseUrl = false
        };

        public override List<ModelDefinition> GetDefaultModels() => ModelCatalog.GetDefaults("Anthropic");

        public override async Task<AiResponse> SendChatAsync(IEnumerable<ChatMessage> messages, CancellationToken cancellationToken = default)
        {
            var sw = Stopwatch.StartNew();

            try
            {
                var prompt = BuildPrompt(messages);
                var result = await RunClaudeAsync(prompt, cancellationToken).ConfigureAwait(false);
                if (result.ExitCode != 0)
                {
                    return new AiResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = BuildFailureMessage(result),
                        Duration = sw.Elapsed
                    };
                }

                var output = ExtractResultText(result.Output);
                if (string.IsNullOrWhiteSpace(output))
                {
                    return new AiResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = "Claude Code returned an empty response.",
                        Duration = sw.Elapsed
                    };
                }

                return new AiResponse
                {
                    Content = output,
                    IsSuccess = true,
                    Duration = sw.Elapsed
                };
            }
            catch (OperationCanceledException)
            {
                return new AiResponse
                {
                    IsSuccess = false,
                    ErrorMessage = "Claude Code request was cancelled.",
                    Duration = sw.Elapsed
                };
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"{GetType().Name}.SendChat", "Claude Code request failed", ex);
                return new AiResponse
                {
                    IsSuccess = false,
                    ErrorMessage = "Claude Code request failed.",
                    Duration = sw.Elapsed
                };
            }
        }

        /// <summary>
        /// Returns the lightweight Claude Code availability probe used by validation and setup tests.
        /// </summary>
        public virtual ClaudeCliStatus GetConnectionStatus()
            => GetCliStatus();

        public override Task<ProviderValidationResult> ValidateConfigurationAsync(CancellationToken cancellationToken = default)
        {
            var status = GetConnectionStatus();
            return Task.FromResult(status.IsInstalled
                ? ProviderValidationResult.Ok()
                : ProviderValidationResult.Fail(status.UserMessage));
        }

        public static ClaudeCliStatus GetCliStatus()
        {
            var resolution = FindClaudeCli();
            if (resolution.Path != null)
            {
                return new ClaudeCliStatus(
                    true,
                    resolution.Path,
                    resolution.UseWslFallback,
                    $"Claude Code detected at {resolution.Path}");
            }

            var message = resolution.UseWslFallback
                ? "Claude Code was not found on the Windows PATH, but WSL is available.\n" +
                  "Install Claude Code in WSL and restart Aire."
                : "Claude Code not found. Install it and make sure 'claude' is available in a terminal, then restart Aire.";

            return new ClaudeCliStatus(false, null, resolution.UseWslFallback, message);
        }

        private static string BuildPrompt(IEnumerable<ChatMessage> messages)
        {
            var messageList = messages?.ToList() ?? [];
            var systemMessages = messageList
                .Where(message => string.Equals(message.Role, "system", StringComparison.OrdinalIgnoreCase))
                .ToList();
            var conversationMessages = messageList
                .Where(message => !string.Equals(message.Role, "system", StringComparison.OrdinalIgnoreCase))
                .ToList();
            var latestUserMessage = conversationMessages
                .LastOrDefault(message => string.Equals(message.Role, "user", StringComparison.OrdinalIgnoreCase))
                ?.Content;
            var requiresToolOnlyResponse = CodexProvider.RequiresForcedToolOnlyResponse(latestUserMessage);
            var isRetryFollowUp = CodexProvider.IsRetryFollowUp(latestUserMessage);
            var conversationWindow = CodexProvider.SelectConversationWindow(conversationMessages, requiresToolOnlyResponse, isRetryFollowUp);

            var sb = new StringBuilder();
            sb.AppendLine("You are Claude Code, answering inside Aire.");
            sb.AppendLine("SYSTEM blocks below are higher priority than USER or ASSISTANT blocks. Obey them exactly.");
            sb.AppendLine("Do not mention internal CLI details unless the user explicitly asks.");
            sb.AppendLine("Never use Claude Code CLI's own file, search, shell, or browser actions when Aire tools are available.");
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
                    sb.AppendLine(TrimForPrompt(message.Content, requiresToolOnlyResponse ? 2500 : 6000));
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
                sb.AppendLine(TrimForPrompt(message.Content, requiresToolOnlyResponse ? 2000 : 4000));
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

        private async Task<(int ExitCode, string Output, string ErrorOutput)> RunClaudeAsync(
            string prompt,
            CancellationToken cancellationToken)
        {
            var status = GetCliStatus();
            if (status.CliPath == null)
                return (-1, string.Empty, status.UserMessage);

            var psi = CreateClaudeProcessStartInfo(status);
            using var process = new Process { StartInfo = psi };
            if (!process.Start())
                throw new InvalidOperationException("Failed to start Claude Code.");

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

            await process.StandardInput.WriteAsync(prompt.AsMemory(), cancellationToken).ConfigureAwait(false);
            await process.StandardInput.FlushAsync().ConfigureAwait(false);
            process.StandardInput.Close();

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

                throw new TimeoutException("Claude Code timed out.");
            }

            return (process.ExitCode, await stdoutTask.ConfigureAwait(false), await stderrTask.ConfigureAwait(false));
        }

        private ProcessStartInfo CreateClaudeProcessStartInfo(ClaudeCliStatus status)
        {
            var model = string.IsNullOrWhiteSpace(Config.Model) ? "default" : Config.Model.Trim();
            var psi = new ProcessStartInfo
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                StandardOutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                StandardErrorEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
            };

            if (status.UseWslFallback)
            {
                psi.FileName = "wsl.exe";
                psi.ArgumentList.Add("claude");
            }
            else if (status.CliPath!.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) ||
                     status.CliPath.EndsWith(".bat", StringComparison.OrdinalIgnoreCase))
            {
                psi.FileName = "cmd.exe";
                psi.ArgumentList.Add("/d");
                psi.ArgumentList.Add("/c");
                psi.ArgumentList.Add(status.CliPath);
            }
            else
            {
                psi.FileName = status.CliPath;
            }

            psi.ArgumentList.Add("-p");
            psi.ArgumentList.Add("--output-format");
            psi.ArgumentList.Add("json");
            psi.ArgumentList.Add("--max-turns");
            psi.ArgumentList.Add("1");

            if (!string.Equals(model, "default", StringComparison.OrdinalIgnoreCase))
            {
                psi.ArgumentList.Add("--model");
                psi.ArgumentList.Add(model);
            }

            if (!status.UseWslFallback)
                psi.WorkingDirectory = GetWorkingDirectory();

            return psi;
        }

        internal static string BuildFailureMessage((int ExitCode, string Output, string ErrorOutput) result)
        {
            var extracted = ExtractResultText(result.Output);
            if (!string.IsNullOrWhiteSpace(extracted))
                return extracted.Trim();

            if (!string.IsNullOrWhiteSpace(result.ErrorOutput))
                return result.ErrorOutput.Trim();

            return $"Claude Code exited with code {result.ExitCode}.";
        }

        private static string ExtractResultText(string output)
        {
            var trimmed = output?.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
                return string.Empty;

            if (!TryExtractFromJson(trimmed, out var extracted))
                return trimmed;

            return extracted ?? trimmed;
        }

        private static bool TryExtractFromJson(string output, out string? extracted)
        {
            extracted = null;

            try
            {
                using var doc = JsonDocument.Parse(output);
                extracted = ExtractResultFromElement(doc.RootElement);
                return !string.IsNullOrWhiteSpace(extracted);
            }
            catch
            {
                return false;
            }
        }

        private static string? ExtractResultFromElement(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                if (TryGetStringProperty(element, "result", out var result) && !string.IsNullOrWhiteSpace(result))
                    return result;

                if (TryGetStringProperty(element, "content", out var content) && !string.IsNullOrWhiteSpace(content))
                    return content;

                if (TryGetStringProperty(element, "message", out var message) && !string.IsNullOrWhiteSpace(message))
                    return message;

                if (element.TryGetProperty("messages", out var messages) && messages.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in messages.EnumerateArray().Reverse())
                    {
                        var nested = ExtractResultFromElement(item);
                        if (!string.IsNullOrWhiteSpace(nested))
                            return nested;
                    }
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray().Reverse())
                {
                    var nested = ExtractResultFromElement(item);
                    if (!string.IsNullOrWhiteSpace(nested))
                        return nested;
                }
            }

            return null;
        }

        private static bool TryGetStringProperty(JsonElement element, string propertyName, out string? value)
        {
            value = null;
            if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
                return false;

            value = property.GetString();
            return !string.IsNullOrWhiteSpace(value);
        }

        private static (string? Path, bool UseWslFallback) FindClaudeCli()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            var preferredCandidates = new[]
            {
                Path.Combine(appData, "npm", "claude.cmd"),
                Path.Combine(appData, "npm", "claude"),
                Path.Combine(userProfile, ".npm-global", "bin", "claude.cmd"),
                Path.Combine(userProfile, ".npm-global", "bin", "claude"),
                Path.Combine(localApp, "Volta", "bin", "claude.cmd"),
                Path.Combine(localApp, "Volta", "bin", "claude"),
                Path.Combine(localApp, "Microsoft", "WindowsApps", "claude.exe"),
                Path.Combine(localApp, "Microsoft", "WindowsApps", "claude.cmd"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs", "claude.cmd"),
            };

            foreach (var candidate in preferredCandidates.Where(path => !string.IsNullOrWhiteSpace(path)))
            {
                if (File.Exists(candidate))
                    return (candidate, false);
            }

            var whereResults = GetWhereResults();
            foreach (var path in whereResults.Where(path => !string.IsNullOrWhiteSpace(path)))
            {
                if (File.Exists(path))
                    return (path, false);
            }

            if (OperatingSystem.IsWindows())
            {
                try
                {
                    var wsl = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "wsl.exe");
                    if (File.Exists(wsl) && IsClaudeAvailableInWsl(wsl))
                        return (wsl, true);
                }
                catch
                {
                }
            }

            return (null, false);
        }

        private static IEnumerable<string> GetWhereResults()
        {
            try
            {
                var wherePsi = new ProcessStartInfo("where.exe", "claude")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
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
            catch
            {
            }

            return Array.Empty<string>();
        }

        private static bool IsClaudeAvailableInWsl(string wslPath)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = wslPath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                psi.ArgumentList.Add("-e");
                psi.ArgumentList.Add("sh");
                psi.ArgumentList.Add("-lc");
                psi.ArgumentList.Add("command -v claude >/dev/null 2>&1");

                using var process = Process.Start(psi);
                if (process == null)
                    return false;

                if (!process.WaitForExit(5000))
                {
                    try
                    {
                        if (!process.HasExited)
                            process.Kill(entireProcessTree: true);
                    }
                    catch
                    {
                    }

                    return false;
                }

                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

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

        private static string TrimForPrompt(string? content, int maxChars)
        {
            if (string.IsNullOrEmpty(content) || content.Length <= maxChars)
                return content ?? string.Empty;

            return content.Substring(0, maxChars) + "\n[Truncated for Claude Code prompt size]";
        }

        public sealed record ClaudeCliStatus(
            bool IsInstalled,
            string? CliPath,
            bool UseWslFallback,
            string UserMessage);
    }
}
