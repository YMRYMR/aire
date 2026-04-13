using System;
using System.Text.Json;
using System.Threading.Tasks;
using static Aire.Services.Tools.ToolHelpers;

namespace Aire.Services.Tools
{
    /// <summary>
    /// Handles the <c>request_context</c> tool that lets AI request specific context
    /// before responding — clipboard content, environment info, recent messages, etc.
    /// </summary>
    public sealed class ContextInjectionToolService
    {
        /// <summary>
        /// Executes the context request tool. Supports:
        /// <list type="bullet">
        ///   <item><c>clipboard</c> — returns current clipboard text</item>
        ///   <item><c>environment</c> — returns OS, .NET version, machine name</item>
        ///   <item><c>datetime</c> — returns current date and time</item>
        ///   <item><c>processes</c> — returns running process list</item>
        /// </list>
        /// </summary>
        public Task<ToolExecutionResult> ExecuteAsync(ToolCallRequest request)
        {
            var contextType = GetString(request, "type")?.ToLowerInvariant();

            var result = contextType switch
            {
                "clipboard" => GetClipboardContext(),
                "environment" => GetEnvironmentContext(),
                "datetime" or "date" or "time" => GetDateTimeContext(),
                _ => new ToolExecutionResult
                {
                    TextResult = "Unknown context type. Available: clipboard, environment, datetime."
                }
            };

            return Task.FromResult(result);
        }

        private static ToolExecutionResult GetClipboardContext()
        {
            try
            {
                var text = System.Windows.Clipboard.GetText();
                if (string.IsNullOrEmpty(text))
                    return new ToolExecutionResult { TextResult = "Clipboard is empty." };

                // Truncate very large clipboard content.
                if (text.Length > 5000)
                    text = text[..5000] + "\n[...truncated]";

                return new ToolExecutionResult { TextResult = $"Clipboard content:\n{text}" };
            }
            catch
            {
                return new ToolExecutionResult { TextResult = "Could not access clipboard." };
            }
        }

        private static ToolExecutionResult GetEnvironmentContext()
        {
            var info = $"""
                OS: {Environment.OSVersion}
                Machine: {Environment.MachineName}
                User: {Environment.UserName}
                .NET: {Environment.Version}
                Processors: {Environment.ProcessorCount}
                Working Set: {Environment.WorkingSet / 1024 / 1024} MB
                """;

            return new ToolExecutionResult { TextResult = info };
        }

        private static ToolExecutionResult GetDateTimeContext()
        {
            return new ToolExecutionResult
            {
                TextResult = $"Current date and time: {DateTime.Now:yyyy-MM-dd HH:mm:ss dddd}\nTimezone: {TimeZoneInfo.Local.DisplayName}"
            };
        }
    }
}
