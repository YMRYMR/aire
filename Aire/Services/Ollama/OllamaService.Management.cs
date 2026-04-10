using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Aire.Services
{
    public partial class OllamaService
    {
        public async Task PullModelAsync(string modelName, string? baseUrl = null,
            IProgress<OllamaPullProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            var url = string.IsNullOrEmpty(baseUrl) ? DefaultBaseUrl : baseUrl.TrimEnd('/');
            var request = new OllamaPullRequest { Name = modelName, Stream = true };
            var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            });

            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            // Use ResponseHeadersRead so the body is streamed as it arrives rather than
            // buffered in memory first — without this, progress events never fire during download.
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{url}/api/pull")
            {
                Content = content
            };
            using var response = await _httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorText = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new HttpRequestException($"Failed to pull model: {errorText}");
            }

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);

            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (line == null)
                    break;
                if (string.IsNullOrEmpty(line))
                    continue;

                try
                {
                    var progressEvent = JsonSerializer.Deserialize<OllamaPullProgress>(line, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
                    });

                    if (progressEvent != null)
                        progress?.Report(progressEvent);
                }
                catch (JsonException)
                {
                    // Partial/malformed SSE line — skip it, more lines will follow
                }
            }
        }

        public async Task DeleteModelAsync(string modelName, string? baseUrl = null, CancellationToken cancellationToken = default)
        {
            var url = string.IsNullOrEmpty(baseUrl) ? DefaultBaseUrl : baseUrl.TrimEnd('/');
            var request = new OllamaDeleteRequest { Name = modelName };
            var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            });

            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var requestMessage = new HttpRequestMessage(HttpMethod.Delete, $"{url}/api/delete")
            {
                Content = content
            };
            using var response = await _httpClient.SendAsync(requestMessage, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorText = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new HttpRequestException($"Failed to delete model: {errorText}");
            }
        }

        public static bool IsOllamaInPath()
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (File.Exists(Path.Combine(localAppData, "Programs", "Ollama", "ollama.exe")))
                return true;

            var paths = Environment.GetEnvironmentVariable("PATH")?.Split(';') ?? Array.Empty<string>();
            return paths.Any(p => File.Exists(Path.Combine(p.Trim(), "ollama.exe")));
        }

        public static bool IsOllamaServiceRunning()
        {
            try
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "sc",
                        Arguments = "query Ollama",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true
                    }
                };
                process.Start();
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                return output.Contains("RUNNING");
            }
            catch
            {
                return false;
            }
        }

        public async Task InstallOllamaAsync(IProgress<int>? progress = null, CancellationToken cancellationToken = default)
        {
            const string installerUrl = "https://ollama.com/download/OllamaSetup.exe";
            var tempPath = Path.GetTempFileName() + ".exe";

            try
            {
                using var response = await _httpClient.GetAsync(installerUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? 0L;
                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var fileStream = File.Create(tempPath);

                var buffer = new byte[81920];
                long totalRead = 0;
                int bytesRead;

                while ((bytesRead = await stream.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                    totalRead += bytesRead;

                    if (totalBytes > 0)
                    {
                        var percent = (int)((double)totalRead / totalBytes * 100);
                        progress?.Report(percent);
                    }
                }

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = tempPath,
                        Arguments = "/S",
                        UseShellExecute = true
                    }
                };

                process.Start();
                await Task.Delay(5000, cancellationToken);
            }
            finally
            {
                try { File.Delete(tempPath); }
                catch (Exception ex)
                {
                    AppLogger.Warn(nameof(OllamaService) + ".PullModelAsync", $"Failed to delete temporary installer '{tempPath}'", ex);
                }
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _httpClient?.Dispose();
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }

    }
}
