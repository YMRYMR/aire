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

        private static readonly string _chatImagesDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Aire", "ChatImages");

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
                        var s when s.Contains("svg")                        => ".svg",
                        var s when s.Contains("bmp")                        => ".bmp",
                        _ => pathOrUrl.Contains(".jpg",  StringComparison.OrdinalIgnoreCase) ? ".jpg"
                           : pathOrUrl.Contains(".gif",  StringComparison.OrdinalIgnoreCase) ? ".gif"
                           : pathOrUrl.Contains(".webp", StringComparison.OrdinalIgnoreCase) ? ".webp"
                           : pathOrUrl.Contains(".svg",  StringComparison.OrdinalIgnoreCase) ? ".svg"
                           : pathOrUrl.Contains(".bmp",  StringComparison.OrdinalIgnoreCase) ? ".bmp"
                           : ".png"
                    };

                    Directory.CreateDirectory(_chatImagesDir);
                    var path = Path.Combine(_chatImagesDir, $"img_{DateTime.Now:yyyyMMdd_HHmmss_fff}{ext}");
                    await File.WriteAllBytesAsync(path, bytes);

                    return new ToolExecutionResult
                    {
                        TextResult     = string.IsNullOrEmpty(caption) ? "Image shown in chat." : caption,
                        ScreenshotPath = path
                    };
                }
        catch
                {
            return new ToolExecutionResult { TextResult = "Error downloading image." };
                }
            }
            else
            {
                if (!File.Exists(pathOrUrl))
                    return new ToolExecutionResult { TextResult = $"Error: File not found: {pathOrUrl}" };

                var managedPath = await StoreLocalImageAsync(pathOrUrl);
                var originalName = Path.GetFileName(pathOrUrl);

                return new ToolExecutionResult
                {
                    TextResult     = string.IsNullOrEmpty(caption) ? $"Showing: {originalName}" : caption,
                    ScreenshotPath = managedPath
                };
            }
        }

        private static async Task<string> StoreLocalImageAsync(string sourcePath)
        {
            Directory.CreateDirectory(_chatImagesDir);

            var extension = Path.GetExtension(sourcePath);
            if (string.IsNullOrWhiteSpace(extension))
                extension = ".png";

            var safeBaseName = Path.GetFileNameWithoutExtension(sourcePath);
            if (string.IsNullOrWhiteSpace(safeBaseName))
                safeBaseName = "img";
            var managedPath = Path.Combine(_chatImagesDir, $"{safeBaseName}_{DateTime.Now:yyyyMMdd_HHmmss_fff}{extension}");

            if (IsPathUnderDirectory(sourcePath, AppContext.BaseDirectory))
            {
                try
                {
                    File.Move(sourcePath, managedPath, overwrite: true);
                    return managedPath;
                }
                catch
                {
                    // Fall back to copy if the source is still locked or cannot be moved.
                }
            }

            await using var source = File.OpenRead(sourcePath);
            await using var target = File.Create(managedPath);
            await source.CopyToAsync(target);
            return managedPath;
        }

        private static bool IsPathUnderDirectory(string path, string directory)
        {
            try
            {
                var fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var fullDirectory = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return fullPath.StartsWith(fullDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(fullPath, fullDirectory, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }
    }
}
