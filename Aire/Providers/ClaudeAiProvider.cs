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

        protected override ToolCallMode DefaultToolCallMode => ToolCallMode.NativeFunctionCalling;
        protected override ToolOutputFormat DefaultToolOutputFormat => ToolOutputFormat.NativeToolCalls;
        protected override bool PreferCompactToolDescriptions => true;

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
                Debug.WriteLine($"Anthropic live model fetch failed: {ex.GetType().Name}");
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
                Debug.WriteLine($"{GetType().Name} chat failed: {ex.GetType().Name}");
                return new AiResponse { IsSuccess = false, ErrorMessage = "Anthropic request failed.", Duration = sw.Elapsed };
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
                    if (m.PreferPromptCache)
                    {
                        return (object)new
                        {
                            role = m.Role,
                            content = new object[]
                            {
                                new { type = "text", text = m.Content, cache_control = new { type = "ephemeral" } }
                            }
                        };
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

            // Inject native tool schemas when tools are enabled
            if (!Config!.SkipNativeTools && ToolCallMode == ToolCallMode.NativeFunctionCalling)
            {
                var tools = SharedToolDefinitions.ToAnthropicTools(
                    Config.ModelCapabilities,
                    Config.EnabledToolCategories,
                    compact: PreferCompactToolDescriptions);
                if (tools.Count > 0)
                    bodyObj["tools"] = tools;
            }

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
                throw new HttpRequestException("Anthropic request failed.");
            }

            await using var stream = await response.Content
                .ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var reader = new StreamReader(stream);

            // State for accumulating tool_use blocks
            string? currentToolName = null;
            var currentToolJson = new StringBuilder();
            bool inToolUseBlock = false;

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

                if (!root.TryGetProperty("type", out var typeEl)) continue;
                var eventType = typeEl.GetString();

                if (eventType == "content_block_start")
                {
                    if (root.TryGetProperty("content_block", out var cb) &&
                        cb.TryGetProperty("type", out var cbType) &&
                        cbType.GetString() == "tool_use")
                    {
                        currentToolName = cb.TryGetProperty("name", out var n) ? n.GetString() : null;
                        currentToolJson.Clear();
                        inToolUseBlock = true;
                    }
                }
                else if (eventType == "content_block_delta" &&
                         root.TryGetProperty("delta", out var delta))
                {
                    var deltaType = delta.TryGetProperty("type", out var dt) ? dt.GetString() : null;

                    if (deltaType == "text_delta" &&
                        delta.TryGetProperty("text", out var text))
                    {
                        var chunk = text.GetString();
                        if (!string.IsNullOrEmpty(chunk))
                            yield return chunk;
                    }
                    else if (deltaType == "input_json_delta" &&
                             inToolUseBlock &&
                             delta.TryGetProperty("partial_json", out var pj))
                    {
                        currentToolJson.Append(pj.GetString());
                    }
                }
                else if (eventType == "content_block_stop" && inToolUseBlock)
                {
                    if (!string.IsNullOrEmpty(currentToolName))
                    {
                        var toolCall = ConvertAnthropicToolUseToToolCall(currentToolName, currentToolJson.ToString());
                        if (!string.IsNullOrEmpty(toolCall))
                            yield return toolCall;
                    }
                    inToolUseBlock = false;
                    currentToolName = null;
                    currentToolJson.Clear();
                }
            }
        }

        // ── Tool call conversion ─────────────────────────────────────────────

        private static string ConvertAnthropicToolUseToToolCall(string toolName, string inputJson)
        {
            try
            {
                using var ms = new System.IO.MemoryStream();
                using var writer = new Utf8JsonWriter(ms);
                writer.WriteStartObject();
                writer.WriteString("tool", toolName);
                if (!string.IsNullOrEmpty(inputJson))
                {
                    using var doc = JsonDocument.Parse(inputJson);
                    foreach (var prop in doc.RootElement.EnumerateObject())
                        prop.WriteTo(writer); // preserves exact JSON type (bool, number, array, object)
                }
                writer.WriteEndObject();
                writer.Flush();
                return $"\n<tool_call>{System.Text.Encoding.UTF8.GetString(ms.ToArray())}</tool_call>";
            }
            catch { return string.Empty; }
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
                catch (Exception ex)
                {
                    Debug.WriteLine($"{GetType().Name} validation failed: {ex.GetType().Name}");
                    return ProviderValidationResult.Fail("Anthropic configuration validation failed.");
                }
            }
            return ProviderValidationResult.Fail("API key is required.");
        }
    }
}
