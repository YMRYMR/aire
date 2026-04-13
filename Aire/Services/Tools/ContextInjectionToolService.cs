using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using static Aire.Services.Tools.ToolHelpers;

namespace Aire.Services.Tools
{
    /// <summary>
    /// Handles the <c>request_context</c> tool that lets AI request specific context
    /// before responding — clipboard content, environment info, file contents, URLs, etc.
    /// </summary>
    public sealed class ContextInjectionToolService
    {
        private static readonly HttpClient HttpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        private const int MaxContentSize = 30_000;

        /// <summary>
        /// Executes the context request tool. Supports:
        /// <list type="bullet">
        ///   <item><c>clipboard</c> — returns current clipboard text</item>
        ///   <item><c>environment</c> — returns OS, .NET version, machine name</item>
        ///   <item><c>datetime</c> — returns current date and time</item>
        ///   <item><c>file</c> — returns text file content (requires "path" parameter)</item>
        ///   <item><c>url</c> — fetches and returns URL content (requires "url" parameter)</item>
        /// </list>
        /// </summary>
        public async Task<ToolExecutionResult> ExecuteAsync(ToolCallRequest request)
        {
            var contextType = GetString(request, "type")?.ToLowerInvariant();

            return contextType switch
            {
                "clipboard" => GetClipboardContext(),
                "environment" => GetEnvironmentContext(),
                "datetime" or "date" or "time" => GetDateTimeContext(),
                "file" => GetFileContext(GetString(request, "path")),
                "url" => await GetUrlContextAsync(GetString(request, "url")),
                _ => new ToolExecutionResult
                {
                    TextResult = "Unknown context type. Available: clipboard, environment, datetime, file, url."
                }
            };
        }

        private static ToolExecutionResult GetClipboardContext()
        {
            try
            {
                var text = System.Windows.Clipboard.GetText();
                if (string.IsNullOrEmpty(text))
                    return new ToolExecutionResult { TextResult = "Clipboard is empty." };

                if (text.Length > MaxContentSize)
                    text = text[..MaxContentSize] + "\n[...truncated]";

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

        private static ToolExecutionResult GetFileContext(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return new ToolExecutionResult { TextResult = "File context requires a \"path\" parameter." };

            try
            {
                if (!File.Exists(path))
                    return new ToolExecutionResult { TextResult = $"File not found: {path}" };

                var ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext is ".exe" or ".dll" or ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp" or ".zip" or ".rar" or ".7z" or ".mp3" or ".mp4" or ".wav" or ".avi")
                    return new ToolExecutionResult { TextResult = $"Binary file skipped: {path} ({ext})" };

                var content = File.ReadAllText(path);
                var sizeInfo = $"File: {path} ({content.Length:N0} chars)\n---\n";

                if (content.Length > MaxContentSize)
                    content = content[..MaxContentSize] + "\n[...truncated]";

                return new ToolExecutionResult { TextResult = sizeInfo + content };
            }
            catch (UnauthorizedAccessException)
            {
                return new ToolExecutionResult { TextResult = $"Access denied: {path}" };
            }
            catch (Exception ex)
            {
                return new ToolExecutionResult { TextResult = $"Error reading file: {ex.Message}" };
            }
        }

        private static async Task<ToolExecutionResult> GetUrlContextAsync(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return new ToolExecutionResult { TextResult = "URL context requires a \"url\" parameter." };

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != "http" && uri.Scheme != "https"))
            {
                return new ToolExecutionResult { TextResult = $"Invalid URL: {url}" };
            }

            try
            {
                var response = await HttpClient.GetAsync(uri);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var header = $"URL: {url} ({content.Length:N0} chars)\n---\n";

                if (content.Length > MaxContentSize)
                    content = content[..MaxContentSize] + "\n[...truncated]";

                return new ToolExecutionResult { TextResult = header + content };
            }
            catch (HttpRequestException ex)
            {
                return new ToolExecutionResult { TextResult = $"HTTP error fetching {url}: {ex.Message}" };
            }
            catch (TaskCanceledException)
            {
                return new ToolExecutionResult { TextResult = $"Request timed out: {url}" };
            }
        }
    }
}
