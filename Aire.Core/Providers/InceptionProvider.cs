using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Aire.Data;
using Aire.Services;

namespace Aire.Providers
{
    /// <summary>
    /// Inception provider (OpenAI‑compatible API hosted by Inception Labs).
    /// </summary>
    public class InceptionProvider : OpenAiProvider
    {
        public override string ProviderType => "Inception";
        public override string DisplayName => "Inception (OpenAI‑compatible)";

        protected override ProviderCapabilities GetBaseCapabilities() =>
            ProviderCapabilities.TextChat    |
            ProviderCapabilities.Streaming   |
            ProviderCapabilities.ToolCalling |
            ProviderCapabilities.SystemPrompt;

        protected override string DefaultApiBaseUrl => "https://api.inceptionlabs.ai";

        public override ProviderFieldHints FieldHints => new() { ShowBaseUrl = false };

        // ── IProviderMetadata overrides ─────────────────────────────────────

        public override async Task<List<ModelDefinition>?> FetchLiveModelsAsync(
            string? apiKey, string? baseUrl, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(apiKey)) return null;
            try
            {
                var url = string.IsNullOrWhiteSpace(baseUrl)
                    ? "https://api.inceptionlabs.ai" : baseUrl.TrimEnd('/');

                using var req = new HttpRequestMessage(HttpMethod.Get, $"{url}/v1/models");
                req.Headers.Add("Authorization", $"Bearer {apiKey}");
                var res = await MetadataHttp.SendAsync(req, ct);
                if (!res.IsSuccessStatusCode) return null;

                var json = await res.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("data", out var dataEl)) return null;

                // Filter for Inception‑specific model IDs (e.g., mercury-*, inception-*)
                var prefixes = new[] { "mercury-", "inception-" };
                return dataEl.EnumerateArray()
                    .Select(m => m.GetProperty("id").GetString() ?? "")
                    .Where(id => !string.IsNullOrEmpty(id) &&
                                 prefixes.Any(p => id.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
                    .OrderByDescending(id => id)
                    .Select(id => new ModelDefinition { Id = id, DisplayName = id })
                    .ToList();
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"{GetType().Name}.FetchLiveModels", "Inception live model fetch failed", ex);
                return null;
            }
        }
    }
}
