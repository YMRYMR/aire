using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Aire.Services
{
    public partial class OllamaService
    {
        public async Task<bool> IsOllamaReachableAsync(string? baseUrl = null, CancellationToken cancellationToken = default)
        {
            var url = string.IsNullOrEmpty(baseUrl) ? DefaultBaseUrl : baseUrl.TrimEnd('/');

            try
            {
                var response = await _httpClient.GetAsync($"{url}/api/tags", cancellationToken);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<List<OllamaModel>> GetInstalledModelsAsync(string? baseUrl = null, CancellationToken cancellationToken = default)
        {
            var url = string.IsNullOrEmpty(baseUrl) ? DefaultBaseUrl : baseUrl.TrimEnd('/');
            List<OllamaModel>? httpModels = null;
            bool httpSuccess = false;

            try
            {
                var response = await _httpClient.GetAsync($"{url}/api/tags", cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync(cancellationToken);
                    Debug.WriteLine($"Ollama API response from {url}: {json.Length} chars");
                    Log($"Ollama API response from {url}: {json.Length} chars");
                    try
                    {
                        var tagsResponse = JsonSerializer.Deserialize<OllamaTagsResponse>(json, new JsonSerializerOptions
                        {
                            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
                        });
                        httpModels = tagsResponse?.Models ?? new List<OllamaModel>();
                        Debug.WriteLine($"Deserialized models count: {httpModels.Count}");
                        Log($"Deserialized models count: {httpModels.Count}");
                        httpSuccess = true;
                    }
                    catch (JsonException ex)
                    {
                        Debug.WriteLine($"JSON deserialization error: {ex.GetType().Name}");
                        Log($"JSON deserialization error: {ex.GetType().Name}");
                    }
                }
                else
                {
                    Debug.WriteLine($"HTTP request failed with status {response.StatusCode}");
                    Log($"HTTP request failed with status {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"HTTP request exception: {ex.GetType().Name}");
                Log($"HTTP request exception: {ex.GetType().Name}");
            }

            if (httpSuccess && httpModels != null && httpModels.Count > 0)
            {
                Debug.WriteLine($"Returning {httpModels.Count} models from HTTP API");
                return httpModels;
            }

            Debug.WriteLine("Falling back to CLI");
            var cliModels = await GetInstalledModelsViaCliAsync(cancellationToken);
            if (cliModels.Count > 0)
            {
                Debug.WriteLine($"Returning {cliModels.Count} models from CLI");
                return cliModels;
            }

            Debug.WriteLine("Both HTTP and CLI failed to retrieve models");
            return new List<OllamaModel>();
        }

        private async Task<List<OllamaModel>> GetInstalledModelsViaCliAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "ollama",
                    Arguments = "list",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process == null)
                    return new List<OllamaModel>();

                await process.WaitForExitAsync(cancellationToken);
                var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
                var error = await process.StandardError.ReadToEndAsync(cancellationToken);

                if (!string.IsNullOrEmpty(error))
                    Debug.WriteLine($"Ollama CLI error: {error}");

                var models = new List<OllamaModel>();
                var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines.Skip(1))
                {
                    var columns = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (columns.Length >= 1)
                        models.Add(new OllamaModel { Name = columns[0] });
                }

                return models;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ollama CLI fallback failed: {ex.GetType().Name}");
                return new List<OllamaModel>();
            }
        }

        public async Task<List<OllamaModel>> GetAvailableModelsAsync(CancellationToken cancellationToken = default)
        {
            var models = new List<OllamaModel>();

            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(6));

                var json = await _httpClient.GetStringAsync("https://ollama.com/api/tags", cts.Token);
                var tagsResponse = JsonSerializer.Deserialize<OllamaTagsResponse>(json, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
                });
                if (tagsResponse?.Models?.Count > 0)
                {
                    foreach (var m in tagsResponse.Models)
                    {
                        if (m.Size == 0 && KnownModelMeta.TryGetValue(m.Name, out var meta))
                            m.Size = meta.SizeBytes;
                        models.Add(m);
                    }
                }
            }
            catch
            {
            }

            var existingNames = models.Select(m => m.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in KnownModelMeta)
            {
                if (!existingNames.Contains(kvp.Key))
                {
                    models.Add(new OllamaModel { Name = kvp.Key, Size = kvp.Value.SizeBytes });
                    existingNames.Add(kvp.Key);
                }
            }

            return models;
        }
    }
}
