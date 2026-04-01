using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using static Aire.Services.Tools.ToolHelpers;

namespace Aire.Services.Tools
{
    /// <summary>
    /// Handles show_image: downloads URL images or returns local file paths.
    /// </summary>
    public class AgentToolService
    {
        private static readonly HttpClient _httpClient = new();

        private static readonly string _screenshotTempDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Aire", "screenshots");

        public async Task<ToolExecutionResult> ExecuteShowImageAsync(ToolCallRequest request)
        {
            var pathOrUrl = GetString(request, "path_or_url");
            var caption   = GetString(request, "caption");

            if (string.IsNullOrWhiteSpace(pathOrUrl))
                return new ToolExecutionResult { TextResult = "Error: path_or_url is required." };

            if (pathOrUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var response = await _httpClient.GetAsync(pathOrUrl);
                    if (!response.IsSuccessStatusCode)
                        return new ToolExecutionResult
                        {
                            TextResult = $"Error downloading image: HTTP {(int)response.StatusCode}"
                        };

                    var bytes       = await response.Content.ReadAsByteArrayAsync();
                    var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
                    var ext = contentType switch
                    {
                        var s when s.Contains("jpeg") || s.Contains("jpg") => ".jpg",
                        var s when s.Contains("gif")                        => ".gif",
                        var s when s.Contains("webp")                       => ".webp",
                        var s when s.Contains("bmp")                        => ".bmp",
                        _ => pathOrUrl.Contains(".jpg",  StringComparison.OrdinalIgnoreCase) ? ".jpg"
                           : pathOrUrl.Contains(".gif",  StringComparison.OrdinalIgnoreCase) ? ".gif"
                           : pathOrUrl.Contains(".webp", StringComparison.OrdinalIgnoreCase) ? ".webp"
                           : pathOrUrl.Contains(".bmp",  StringComparison.OrdinalIgnoreCase) ? ".bmp"
                           : ".png"
                    };

                    Directory.CreateDirectory(_screenshotTempDir);
                    var path = Path.Combine(_screenshotTempDir, $"img_{DateTime.Now:yyyyMMdd_HHmmss_fff}{ext}");
                    await File.WriteAllBytesAsync(path, bytes);

                    return new ToolExecutionResult
                    {
                        TextResult     = string.IsNullOrEmpty(caption) ? "Image shown in chat." : caption,
                        ScreenshotPath = path
                    };
                }
                catch (Exception ex)
                {
                    return new ToolExecutionResult { TextResult = $"Error downloading image: {ex.Message}" };
                }
            }
            else
            {
                if (!File.Exists(pathOrUrl))
                    return new ToolExecutionResult { TextResult = $"Error: File not found: {pathOrUrl}" };

                return new ToolExecutionResult
                {
                    TextResult     = string.IsNullOrEmpty(caption) ? $"Showing: {Path.GetFileName(pathOrUrl)}" : caption,
                    ScreenshotPath = pathOrUrl
                };
            }
        }
    }
}
