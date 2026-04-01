using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Aire.Data;
using Aire.Domain.Providers;

namespace Aire.Providers
{
    /// <summary>
    /// Anthropic API provider using direct API-key authentication.
    /// </summary>
    public class ClaudeAiProvider : BaseAiProvider
    {
        private const int MaxSupportedTimeoutMinutes = 35791;
        private readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(5) };

        internal TimeSpan ConfiguredTimeout => _http.Timeout;

        private const string ApiBase          = "https://api.anthropic.com";
        private const string AnthropicVersion = "2023-06-01";

        public override string ProviderType => "Anthropic";
        public override string DisplayName  => "Anthropic API";
        protected override ProviderCapabilities GetBaseCapabilities() =>
            ProviderCapabilities.TextChat    |
            ProviderCapabilities.Streaming   |
            ProviderCapabilities.ImageInput  |
            ProviderCapabilities.ToolCalling |
            ProviderCapabilities.SystemPrompt;

        // ── IProviderMetadata overrides ─────────────────────────────────────

        public override ProviderFieldHints FieldHints => new()
        {
            ApiKeyRequired = false,
        };

        public override void Initialize(ProviderConfig config)
        {
            base.Initialize(config);
            _http.Timeout = TimeSpan.FromMinutes(Math.Clamp(config.TimeoutMinutes, 1, MaxSupportedTimeoutMinutes));
        }

        private static readonly HttpClient _metaHttp = new() { Timeout = TimeSpan.FromSeconds(8) };

        public override async Task<List<ModelDefinition>?> FetchLiveModelsAsync(
            string? apiKey, string? baseUrl, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(apiKey)) return null;
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, "https://api.anthropic.com/v1/models");
                req.Headers.Add("x-api-key", apiKey);
                req.Headers.Add("anthropic-version", "2023-06-01");
                var res = await _metaHttp.SendAsync(req, ct);
                if (!res.IsSuccessStatusCode) return null;

                var json = await res.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("data", out var dataEl)) return null;

                return dataEl.EnumerateArray()
                    .Select(m =>
                    {
                        var id = m.GetProperty("id").GetString() ?? "";
                        var displayName = m.TryGetProperty("display_name", out var dn)
                            ? dn.GetString() ?? id : id;
                        return new ModelDefinition { Id = id, DisplayName = displayName };
                    })
                    .Where(m => !string.IsNullOrEmpty(m.Id))
                    .ToList();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Anthropic live model fetch failed: {ex.Message}");
                return null;
            }
        }

        public override async Task<TokenUsage?> GetTokenUsageAsync(CancellationToken ct)
        {
            // Only works with API key, not browser session.
            if (!TryGetApiKey(out var apiKey))
                return null;

            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, "https://api.anthropic.com/v1/usage");
                req.Headers.Add("x-api-key", apiKey);
                req.Headers.Add("anthropic-version", "2023-06-01");
                var response = await _metaHttp.SendAsync(req, ct);
                if (!response.IsSuccessStatusCode)
                    return null;

                var json = await response.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (!root.TryGetProperty("current_usage", out var usageEl) ||
                    !root.TryGetProperty("limit", out var limitEl))
                    return null;

                var used = usageEl.GetInt64();
                var limit = limitEl.GetInt64();
                DateTime? resetDate = null;
                if (root.TryGetProperty("reset_date", out var resetEl) &&
                    DateTime.TryParse(resetEl.GetString(), out var reset))
                    resetDate = reset;

                return new TokenUsage
                {
                    Used = used,
                    Limit = limit,
                    ResetDate = resetDate,
                    Unit = "tokens"
                };
            }
            catch
            {
                return null;
            }
        }

        private bool TryGetApiKey(out string key)
        {
            var envKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
            if (!string.IsNullOrWhiteSpace(envKey)) { key = envKey; return true; }

            if (!string.IsNullOrWhiteSpace(Config?.ApiKey))
            { key = Config.ApiKey; return true; }

            key = string.Empty;
            return false;
        }

        public override async Task<AiResponse> SendChatAsync(
            IEnumerable<ChatMessage> messages, CancellationToken cancellationToken = default)
        {
            var sw = Stopwatch.StartNew();
            var sb = new StringBuilder();
            try
            {
                await foreach (var chunk in StreamChatAsync(messages, cancellationToken).ConfigureAwait(false))
                    sb.Append(chunk);
                return new AiResponse { Content = sb.ToString(), IsSuccess = true, Duration = sw.Elapsed };
            }
            catch (Exception ex)
            {
                return new AiResponse { IsSuccess = false, ErrorMessage = ex.Message, Duration = sw.Elapsed };
            }
        }

        public override async IAsyncEnumerable<string> StreamChatAsync(
            IEnumerable<ChatMessage> messages,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (TryGetApiKey(out var apiKey))
            {
                await foreach (var chunk in StreamViaApiAsync(apiKey, messages, cancellationToken))
                    yield return chunk;
                yield break;
            }

            throw new InvalidOperationException(
                "Anthropic API is not configured. Set the ANTHROPIC_API_KEY environment variable or enter an API key in Settings.");
        }

        // ── Mode 1: direct API ────────────────────────────────────────────────

        private async IAsyncEnumerable<string> StreamViaApiAsync(
            string apiKey, IEnumerable<ChatMessage> messages,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var msgList = messages.ToList();

            var systemContent = string.Join("\n\n",
                msgList.Where(m => m.Role == "system").Select(m => m.Content));

            var conversation = msgList
                .Where(m => m.Role != "system")
                .Select(m =>
                {
                    if (m.ImageBytes != null && m.ImageBytes.Length > 0)
                    {
                        var mime = m.ImageMimeType ?? "image/png";
                        var b64  = Convert.ToBase64String(m.ImageBytes);
                        object[] blocks = string.IsNullOrEmpty(m.Content)
                            ? new object[]
                            {
                                new { type = "image", source = new { type = "base64", media_type = mime, data = b64 } }
                            }
                            : new object[]
                            {
                                new { type = "text", text = m.Content },
                                new { type = "image", source = new { type = "base64", media_type = mime, data = b64 } }
                            };
                        return (object)new { role = m.Role, content = blocks };
                    }
                    return (object)new { role = m.Role, content = m.Content };
                })
                .ToArray();

            var bodyObj = new Dictionary<string, object>
            {
                ["model"]      = Config?.Model ?? "claude-sonnet-4-5",
                ["max_tokens"] = Config?.MaxTokens > 0 ? Config.MaxTokens : 16384,
                ["stream"]     = true,
                ["messages"]   = conversation
            };
            if (!string.IsNullOrWhiteSpace(systemContent))
                bodyObj["system"] = new object[]
                {
                    new { type = "text", text = systemContent, cache_control = new { type = "ephemeral" } }
                };

            var jsonBody = JsonSerializer.Serialize(bodyObj);

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{ApiBase}/v1/messages");
            request.Headers.Add("x-api-key", apiKey);
            request.Headers.Add("anthropic-version", AnthropicVersion);
            request.Headers.Add("anthropic-beta", "prompt-caching-2024-07-31");
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            using var response = await _http
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                throw new Exception($"Anthropic API {(int)response.StatusCode}: {err}");
            }

            await using var stream = await response.Content
                .ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var reader = new StreamReader(stream);

            string? line;
            while ((line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) != null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!line.StartsWith("data: ")) continue;

                var data = line[6..];
                if (data == "[DONE]") break;

                JsonElement root;
                try { using var doc = JsonDocument.Parse(data); root = doc.RootElement.Clone(); }
                catch { continue; }

                if (root.TryGetProperty("type", out var typeEl) &&
                    typeEl.GetString() == "content_block_delta" &&
                    root.TryGetProperty("delta", out var delta) &&
                    delta.TryGetProperty("text", out var text))
                {
                    var chunk = text.GetString();
                    if (!string.IsNullOrEmpty(chunk))
                        yield return chunk;
                }
            }
        }

        // ── Validation ────────────────────────────────────────────────────────

        public override async Task<ProviderValidationResult> ValidateConfigurationAsync(CancellationToken cancellationToken = default)
        {
            if (TryGetApiKey(out var key))
            {
                try
                {
                    using var req = new HttpRequestMessage(HttpMethod.Get, $"{ApiBase}/v1/models");
                    req.Headers.Add("x-api-key", key);
                    req.Headers.Add("anthropic-version", AnthropicVersion);
                    var res = await _http.SendAsync(req, cancellationToken).ConfigureAwait(false);
                    return res.IsSuccessStatusCode
                        ? ProviderValidationResult.Ok()
                        : ProviderValidationResult.Fail($"HTTP {(int)res.StatusCode} {res.ReasonPhrase}");
                }
                catch (Exception ex) { return ProviderValidationResult.Fail($"{ex.GetType().Name}: {ex.Message}"); }
            }
            return ProviderValidationResult.Fail("API key is required.");
        }
    }
}
