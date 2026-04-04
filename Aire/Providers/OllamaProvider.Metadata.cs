using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Aire.Data;
using Aire.Domain.Providers;

namespace Aire.Providers
{
    public partial class OllamaProvider
    {
        public override async Task<List<ModelDefinition>?> FetchLiveModelsAsync(
            string? apiKey, string? baseUrl, CancellationToken ct)
        {
            var url = string.IsNullOrWhiteSpace(baseUrl) ? "http://localhost:11434" : baseUrl.TrimEnd('/');
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, $"{url}/api/tags");
                if (!string.IsNullOrWhiteSpace(apiKey))
                    req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

                var res = await _metaHttp.SendAsync(req, ct);
                if (!res.IsSuccessStatusCode)
                    return null;

                var json = await res.Content.ReadAsStringAsync(ct);
                var tagsResponse = JsonSerializer.Deserialize<OllamaTagsResponse>(json, DeserializeOpts);
                if (tagsResponse?.Models == null)
                    return null;

                var defaults = ModelCatalog.GetDefaults("Ollama");
                var installedSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var installedModels = new List<ModelDefinition>();

                foreach (var model in tagsResponse.Models)
                {
                    var normalized = model.Name.EndsWith(":latest", StringComparison.OrdinalIgnoreCase)
                        ? model.Name[..^7]
                        : model.Name;

                    installedSet.Add(normalized);
                    installedModels.Add(new ModelDefinition
                    {
                        Id = normalized,
                        DisplayName = normalized,
                        SizeBytes = model.Size,
                        IsInstalled = true,
                    });
                }

                var merged = new List<ModelDefinition>(installedModels);
                foreach (var model in defaults)
                {
                    if (!installedSet.Contains(model.Id))
                        merged.Add(model);
                }

                return merged;
            }
            catch (Exception ex)
            {
            Debug.WriteLine($"Ollama live model fetch failed: {ex.GetType().Name}");
                return null;
            }
        }

        public override void Initialize(ProviderConfig config)
        {
            base.Initialize(config);
            if (!string.IsNullOrWhiteSpace(config.BaseUrl))
                _baseUrl = config.BaseUrl.TrimEnd('/');

            _requestTimeout = TimeSpan.FromMinutes(Math.Clamp(config.TimeoutMinutes, 1, MaxSupportedTimeoutMinutes));
            _httpClient.Timeout = _requestTimeout;

            _apiKey = !string.IsNullOrWhiteSpace(config.ApiKey)
                ? config.ApiKey.Trim()
                : Environment.GetEnvironmentVariable("OLLAMA_API_KEY") ?? string.Empty;

            _toolDefinitions = SharedToolDefinitions.ToOllamaTools(
                config.ModelCapabilities,
                config.EnabledToolCategories);
        }

        public override async Task<ProviderValidationResult> ValidateConfigurationAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _httpClient.SendAsync(BuildRequest(HttpMethod.Get, $"{_baseUrl}/api/tags"), cancellationToken);
                if (!response.IsSuccessStatusCode)
                    return ProviderValidationResult.Fail($"Ollama returned HTTP {response.StatusCode}.");

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var tagsResponse = JsonSerializer.Deserialize<OllamaTagsResponse>(json, DeserializeOpts);

                var modelFound = tagsResponse?.Models?.Any(m =>
                    m.Name.Equals(Config.Model, StringComparison.OrdinalIgnoreCase) ||
                    m.Name.StartsWith(Config.Model + ":", StringComparison.OrdinalIgnoreCase)) ?? false;

                return modelFound
                    ? ProviderValidationResult.Ok()
                    : ProviderValidationResult.Fail($"Model '{Config.Model}' not found in Ollama.");
            }
            catch (Exception ex)
            {
            System.Diagnostics.Debug.WriteLine($"[WARN] [{GetType().Name}.ValidateConfiguration] {ex.GetType().Name}");
                return ProviderValidationResult.Fail("Ollama connection failed.");
            }
        }
    }
}
