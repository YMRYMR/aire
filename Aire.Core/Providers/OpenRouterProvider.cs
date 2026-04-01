using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Aire.Data;

namespace Aire.Providers
{
    /// <summary>
    /// OpenRouter provider (OpenAI-compatible API, many free models available).
    /// </summary>
    public class OpenRouterProvider : OpenAiProvider
    {
        public override string ProviderType => "OpenRouter";
        public override string DisplayName  => "OpenRouter";

        protected override string DefaultApiBaseUrl => "https://openrouter.ai/api";

        public override ProviderFieldHints FieldHints => new() { ShowBaseUrl = false };

        public override async Task<List<ModelDefinition>?> FetchLiveModelsAsync(
            string? apiKey, string? baseUrl, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(apiKey)) return null;
            try
            {
                var url = string.IsNullOrWhiteSpace(baseUrl) ? DefaultApiBaseUrl : baseUrl.TrimEnd('/');
                using var req = new HttpRequestMessage(HttpMethod.Get, $"{url}/v1/models");
                req.Headers.Add("Authorization", $"Bearer {apiKey}");
                var res = await MetadataHttp.SendAsync(req, ct).ConfigureAwait(false);
                if (!res.IsSuccessStatusCode) return null;
                var json = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("data", out var data)) return null;
                return data.EnumerateArray()
                    .Select(m => m.GetProperty("id").GetString() ?? "")
                    .Where(id => !string.IsNullOrEmpty(id))
                    .OrderBy(id => id)
                    .Select(id =>
                    {
                        bool isFree = id.EndsWith(":free", StringComparison.OrdinalIgnoreCase);
                        string display = isFree
                            ? $"{id[..^":free".Length]}  ·  free"
                            : $"{id}  ·  paid";
                        var caps = isFree ? new List<string>() : new List<string> { "tools" };
                        return new ModelDefinition { Id = id, DisplayName = display, Capabilities = caps };
                    })
                    .ToList();
            }
            catch { return null; }
        }
    }
}
