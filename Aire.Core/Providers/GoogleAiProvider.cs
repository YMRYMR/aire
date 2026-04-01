using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Aire.Domain.Providers;
using Aire.Data;

namespace Aire.Providers
{
    public class GoogleAiProvider : BaseAiProvider
    {
        private static readonly HttpClient _http = new();

        public override string ProviderType => "GoogleAI";
        public override string DisplayName  => "Google AI (Gemini)";
        protected override ToolCallMode DefaultToolCallMode => ToolCallMode.NativeFunctionCalling;
        protected override ProviderCapabilities GetBaseCapabilities() =>
            ProviderCapabilities.TextChat    |
            ProviderCapabilities.Streaming   |
            ProviderCapabilities.ImageInput  |
            ProviderCapabilities.SystemPrompt |
            ProviderCapabilities.ToolCalling;

        // ── IProviderMetadata overrides ─────────────────────────────────────

        public override ProviderFieldHints FieldHints => new()
        {
            ShowBaseUrl = false,
        };

        private string ApiBase => string.IsNullOrWhiteSpace(Config.BaseUrl)
            ? "https://generativelanguage.googleapis.com"
            : Config.BaseUrl.TrimEnd('/');

        private string StreamUrl =>
            $"{ApiBase}/v1beta/models/{Config.Model}:streamGenerateContent?key={Config.ApiKey}&alt=sse";

        private string BuildBody(IEnumerable<ChatMessage> messages)
        {
            string? systemText = null;
            var contents = new List<object>();

            foreach (var msg in messages)
            {
                if (msg.Role == "system")
                {
                    systemText = msg.Content;
                    continue;
                }
                contents.Add(new
                {
                    role  = msg.Role == "assistant" ? "model" : "user",
                    parts = new[] { new { text = msg.Content } }
                });
            }

            var genConfig = new { temperature = Config.Temperature, maxOutputTokens = Config.MaxTokens };
            var tools     = new[]
            {
                new
                {
                    function_declarations = SharedToolDefinitions.ToGeminiFunctionDeclarations(
                        Config.ModelCapabilities,
                        Config.EnabledToolCategories)
                }
            };

            if (systemText != null)
                return JsonSerializer.Serialize(new
                {
                    system_instruction = new { parts = new[] { new { text = systemText } } },
                    contents,
                    tools,
                    generationConfig = genConfig,
                });

            return JsonSerializer.Serialize(new
            {
                contents,
                tools,
                generationConfig = genConfig,
            });
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
            var req = new HttpRequestMessage(HttpMethod.Post, StreamUrl)
            {
                Content = new StringContent(BuildBody(messages), Encoding.UTF8, "application/json")
            };

            var resp = await _http
                .SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                var errBody = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                throw new HttpRequestException(
                    $"Gemini API {(int)resp.StatusCode}: {errBody}", null, resp.StatusCode);
            }

            using var stream = await resp.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var reader = new System.IO.StreamReader(stream);

            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (line == null) break;
                if (!line.StartsWith("data: ")) continue;

                var data = line[6..].Trim();
                if (data == "[DONE]") break;

                JsonElement root = default;
                bool hasData = false;
                try
                {
                    using var doc = JsonDocument.Parse(data);
                    root    = doc.RootElement.Clone();
                    hasData = true;
                }
                catch { }

                if (!hasData) continue;
                if (!root.TryGetProperty("candidates", out var candidates)) continue;
                if (candidates.GetArrayLength() == 0) continue;

                var candidate = candidates[0];
                if (!candidate.TryGetProperty("content", out var content)) continue;
                if (!content.TryGetProperty("parts", out var parts)) continue;

                foreach (var part in parts.EnumerateArray())
                {
                    // Regular text chunk
                    if (part.TryGetProperty("text", out var textProp))
                    {
                        var partText = textProp.GetString();
                        if (!string.IsNullOrEmpty(partText))
                            yield return partText;
                        continue;
                    }

                    // Native function call — convert to <tool_call> text so ToolCallParser works
                    if (part.TryGetProperty("functionCall", out var fc))
                    {
                        var name = fc.TryGetProperty("name", out var n) ? n.GetString() : null;
                        if (string.IsNullOrEmpty(name)) continue;

                        var dict = new Dictionary<string, object?> { ["tool"] = name };
                        if (fc.TryGetProperty("args", out var args) && args.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var prop in args.EnumerateObject())
                            {
                                dict[prop.Name] = prop.Value.ValueKind == JsonValueKind.String
                                    ? (object?)prop.Value.GetString()
                                    : (object?)prop.Value.GetRawText();
                            }
                        }
                        yield return $"\n<tool_call>{JsonSerializer.Serialize(dict)}</tool_call>";
                    }
                }
            }
        }

        public override async Task<ProviderValidationResult> ValidateConfigurationAsync(CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(Config.ApiKey))
                return ProviderValidationResult.Fail("API key is required.");
            try
            {
                var req  = new HttpRequestMessage(HttpMethod.Get,
                               $"{ApiBase}/v1beta/models?key={Config.ApiKey}");
                var resp = await _http.SendAsync(req, cancellationToken).ConfigureAwait(false);
                return resp.IsSuccessStatusCode
                    ? ProviderValidationResult.Ok()
                    : ProviderValidationResult.Fail($"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WARN] [{GetType().Name}.ValidateConfiguration] {ex.GetType().Name}: {ex.Message}");
                return ProviderValidationResult.Fail($"{ex.GetType().Name}: {ex.Message}");
            }
        }

        // ── Live model list ──────────────────────────────────────────────────

        public override async Task<List<ModelDefinition>?> FetchLiveModelsAsync(
            string? apiKey, string? baseUrl, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(apiKey)) return null;
            try
            {
                var apiBase = string.IsNullOrWhiteSpace(baseUrl)
                    ? "https://generativelanguage.googleapis.com"
                    : baseUrl.TrimEnd('/');

                var resp = await _http
                    .GetAsync($"{apiBase}/v1beta/models?key={apiKey}", ct)
                    .ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode) return null;

                var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("models", out var arr)) return null;

                var models = new List<ModelDefinition>();
                foreach (var m in arr.EnumerateArray())
                {
                    // name is "models/gemini-2.0-flash" — strip the prefix
                    var fullName    = m.TryGetProperty("name", out var nProp) ? nProp.GetString() : null;
                    var displayName = m.TryGetProperty("displayName", out var dProp) ? dProp.GetString() : null;
                    if (string.IsNullOrEmpty(fullName)) continue;

                    var id = fullName.StartsWith("models/", StringComparison.Ordinal)
                        ? fullName[7..] : fullName;

                    // Only show generative (gemini-*) models; skip embedding, aqa, etc.
                    if (!id.StartsWith("gemini", StringComparison.OrdinalIgnoreCase)) continue;

                    // Skip deprecated / tuned variants
                    var supportedActions = m.TryGetProperty("supportedGenerationMethods", out var sa)
                        ? sa.EnumerateArray().Select(x => x.GetString()).ToList()
                        : new List<string?>();
                    if (!supportedActions.Contains("generateContent")) continue;

                    models.Add(new ModelDefinition { Id = id, DisplayName = displayName ?? id });
                }

                // Sort: latest first (gemini-2.x before 1.x)
                models.Sort((a, b) => string.Compare(b.Id, a.Id, StringComparison.OrdinalIgnoreCase));
                return models.Count > 0 ? models : null;
            }
            catch { return null; }
        }
    }
}
