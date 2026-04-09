using System.Collections.Generic;
using System.Diagnostics;
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

        public override async Task<TokenUsage?> GetTokenUsageAsync(CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(Config.ApiKey))
                return null;

            try
            {
                var url = Config.BaseUrl?.TrimEnd('/') ?? DefaultApiBaseUrl;
                using var req = new HttpRequestMessage(HttpMethod.Get, $"{url}/v1/key");
                req.Headers.Add("Authorization", $"Bearer {Config.ApiKey}");

                var res = await MetadataHttp.SendAsync(req, ct).ConfigureAwait(false);
                if (!res.IsSuccessStatusCode)
                    return null;

                var json = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (!TryReadLong(root, "usage", out var used))
                    return null;

                long? limit = null;
                if (TryReadLong(root, "limit", out var limitValue))
                    limit = limitValue;
                else if (TryReadLong(root, "limit_remaining", out var remainingValue))
                    limit = used + remainingValue;

                DateTime? resetDate = null;
                if (root.TryGetProperty("reset_date", out var resetEl) &&
                    DateTime.TryParse(resetEl.GetString(), out var reset))
                    resetDate = reset;

                return new TokenUsage
                {
                    Used = used,
                    Limit = limit,
                    ResetDate = resetDate,
                    Unit = "credits"
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OpenRouter usage lookup failed: {ex.GetType().Name}");
                return null;
            }
        }

        private static bool TryReadLong(JsonElement root, string propertyName, out long value)
        {
            value = 0;
            if (!root.TryGetProperty(propertyName, out var el))
                return false;

            return el.ValueKind switch
            {
                JsonValueKind.Number => el.TryGetInt64(out value),
                JsonValueKind.String => long.TryParse(el.GetString(), out value),
                _ => false,
            };
        }
    }
}
