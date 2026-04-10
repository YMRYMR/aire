using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Aire.Domain.Providers;
using Aire.Data;
using Aire.Services;

namespace Aire.Providers
{
    public class GoogleAiProvider : BaseAiProvider, IImageGenerationProvider
    {
        private static readonly HttpClient _http = new();
        private readonly Dictionary<string, string> _cachedContentNames = new(StringComparer.Ordinal);

        public override string ProviderType => "GoogleAI";
        public override string DisplayName  => "Google AI (Gemini)";
        protected override ToolCallMode DefaultToolCallMode => ToolCallMode.NativeFunctionCalling;
        protected override bool PreferCompactToolDescriptions => true;
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

        private string GenerateContentUrl =>
            $"{ApiBase}/v1beta/models/{Config.Model}:generateContent";

        public bool SupportsImageGeneration =>
            Config.ModelCapabilities?.Contains("imagegeneration", StringComparer.OrdinalIgnoreCase) == true;

        private string BuildBody(
            IEnumerable<ChatMessage> messages,
            string? cachedContentName = null,
            bool includeTools = true,
            bool includeSystemInstruction = true)
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
            object body;
            if (!string.IsNullOrWhiteSpace(cachedContentName))
            {
                body = new
                {
                    contents,
                    cachedContent = cachedContentName,
                    generationConfig = genConfig,
                };
            }
            else
            {
                var bodyFields = new Dictionary<string, object>
                {
                    ["contents"] = contents,
                    ["generationConfig"] = genConfig
                };

                if (includeTools)
                {
                    bodyFields["tools"] = new[]
                    {
                        new
                        {
                            function_declarations = SharedToolDefinitions.ToGeminiFunctionDeclarations(
                                Config.ModelCapabilities,
                                Config.EnabledToolCategories,
                                compact: PreferCompactToolDescriptions)
                        }
                    };
                }

                if (includeSystemInstruction && systemText != null)
                    bodyFields["system_instruction"] = new { parts = new[] { new { text = systemText } } };

                body = bodyFields;
            }

            return JsonSerializer.Serialize(body);
        }

        private async Task<string?> EnsureCachedContentAsync(
            IReadOnlyList<ChatMessage> prefixMessages,
            string? systemText,
            CancellationToken cancellationToken)
        {
            if (prefixMessages.Count == 0)
                return null;

            var cacheKey = BuildCacheKey(prefixMessages, systemText);
            if (_cachedContentNames.TryGetValue(cacheKey, out var existing))
                return existing;

            var cacheBody = new Dictionary<string, object>
            {
                ["model"] = $"models/{Config.Model}",
                ["contents"] = prefixMessages.Select(ToGeminiContent).ToArray(),
                ["ttl"] = "3600s"
            };

            if (!string.IsNullOrWhiteSpace(systemText))
                cacheBody["systemInstruction"] = new
                {
                    parts = new[] { new { text = systemText } }
                };

            var functionDeclarations = SharedToolDefinitions.ToGeminiFunctionDeclarations(
                Config.ModelCapabilities,
                Config.EnabledToolCategories,
                compact: PreferCompactToolDescriptions);
            if (functionDeclarations.Count > 0)
            {
                cacheBody["tools"] = new[]
                {
                    new
                    {
                        function_declarations = functionDeclarations
                    }
                };
            }

            using var req = new HttpRequestMessage(HttpMethod.Post, $"{ApiBase}/v1beta/cachedContents?key={Config.ApiKey}")
            {
                Content = new StringContent(JsonSerializer.Serialize(cacheBody), Encoding.UTF8, "application/json")
            };

            using var resp = await _http.SendAsync(req, cancellationToken).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
                return null;

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
            if (!doc.RootElement.TryGetProperty("name", out var nameProp))
                return null;

            var name = nameProp.GetString();
            if (string.IsNullOrWhiteSpace(name))
                return null;

            _cachedContentNames[cacheKey] = name;
            return name;
        }

        private static object ToGeminiContent(ChatMessage message) => new
        {
            role = message.Role == "assistant" ? "model" : "user",
            parts = new[] { new { text = message.Content } }
        };

        private string BuildCacheKey(IReadOnlyList<ChatMessage> prefixMessages, string? systemText)
        {
            var functionDeclarations = SharedToolDefinitions.ToGeminiFunctionDeclarations(
                Config.ModelCapabilities,
                Config.EnabledToolCategories,
                compact: PreferCompactToolDescriptions);

            var payload = JsonSerializer.Serialize(new
            {
                model = Config.Model,
                baseUrl = ApiBase,
                systemText,
                tools = functionDeclarations,
                prefix = prefixMessages.Select(m => new { m.Role, m.Content })
            });

            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
            return Convert.ToHexString(hash);
        }

        private (List<ChatMessage> Prefix, List<ChatMessage> Suffix, string? SystemText) SplitCachedPrefix(IReadOnlyList<ChatMessage> messages)
        {
            string? systemText = null;
            var prefix = new List<ChatMessage>();
            var suffix = new List<ChatMessage>();
            var stillPrefix = true;

            foreach (var message in messages)
            {
                if (message.Role == "system")
                {
                    systemText = string.IsNullOrWhiteSpace(systemText)
                        ? message.Content
                        : $"{systemText}\n\n{message.Content}";
                    continue;
                }

                if (stillPrefix &&
                    message.PreferPromptCache &&
                    message.ImageBytes == null &&
                    string.IsNullOrWhiteSpace(message.ImagePath))
                {
                    prefix.Add(message);
                    continue;
                }

                stillPrefix = false;
                suffix.Add(message);
            }

            return (prefix, suffix, systemText);
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
                AppLogger.Warn($"{GetType().Name}.SendChat", "Google AI chat failed", ex);
                return new AiResponse { IsSuccess = false, ErrorMessage = "Google AI request failed.", Duration = sw.Elapsed };
            }
        }

        public override async IAsyncEnumerable<string> StreamChatAsync(
            IEnumerable<ChatMessage> messages,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var messageList = messages.ToList();
            string? requestBody;

            var (prefix, suffix, systemText) = SplitCachedPrefix(messageList);
            var shouldUseExplicitCache =
                Config.ModelCapabilities?.Any(c =>
                    c.Equals("tools", StringComparison.OrdinalIgnoreCase) ||
                    c.Equals("toolcalling", StringComparison.OrdinalIgnoreCase)) != true &&
                Config.EnabledToolCategories is not { Count: > 0 } &&
                prefix.Count > 0;

            if (shouldUseExplicitCache)
            {
                var cachedContentName = await EnsureCachedContentAsync(prefix, systemText, cancellationToken).ConfigureAwait(false);
                requestBody = !string.IsNullOrWhiteSpace(cachedContentName)
                    ? BuildBody(suffix, cachedContentName, includeTools: false, includeSystemInstruction: false)
                    : BuildBody(messageList);
            }
            else
            {
                requestBody = BuildBody(messageList);
            }

            using var req = new HttpRequestMessage(HttpMethod.Post, StreamUrl)
            {
                Content = new StringContent(requestBody, Encoding.UTF8, "application/json")
            };

            using var resp = await _http
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
                using var resp = await _http.SendAsync(req, cancellationToken).ConfigureAwait(false);
                return resp.IsSuccessStatusCode
                    ? ProviderValidationResult.Ok()
                    : ProviderValidationResult.Fail($"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}");
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"{GetType().Name}.ValidateConfiguration", "Google AI configuration validation failed", ex);
                return ProviderValidationResult.Fail("Google AI configuration validation failed.");
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

        public async Task<ImageGenerationResult> GenerateImageAsync(string prompt, CancellationToken cancellationToken = default)
        {
            if (!SupportsImageGeneration)
            {
                return new ImageGenerationResult
                {
                    IsSuccess = false,
                    ErrorMessage = "The selected model does not support image generation."
                };
            }

            if (string.IsNullOrWhiteSpace(Config.ApiKey))
            {
                return new ImageGenerationResult
                {
                    IsSuccess = false,
                    ErrorMessage = "API key is required."
                };
            }

            var payload = JsonSerializer.Serialize(new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = prompt }
                        }
                    }
                },
                generationConfig = new
                {
                    responseModalities = new[] { "IMAGE" }
                }
            });

            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, GenerateContentUrl)
                {
                    Content = new StringContent(payload, Encoding.UTF8, "application/json")
                };
                req.Headers.Add("x-goog-api-key", Config.ApiKey);
                req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                using var response = await _http.SendAsync(req, cancellationToken).ConfigureAwait(false);
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    return new ImageGenerationResult
                    {
                        IsSuccess = false,
                        ErrorMessage = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}: {body}"
                    };
                }

                using var doc = JsonDocument.Parse(body);
                if (!TryExtractGeneratedImage(doc.RootElement, out var imageResult))
                {
                    return new ImageGenerationResult
                    {
                        IsSuccess = false,
                        ErrorMessage = "Image generation returned no image data."
                    };
                }

                return imageResult;
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"{GetType().Name}.GenerateImage", "Google AI image generation failed", ex);
                return new ImageGenerationResult
                {
                    IsSuccess = false,
                    ErrorMessage = "Google AI image generation failed."
                };
            }
        }

        private static bool TryExtractGeneratedImage(JsonElement root, out ImageGenerationResult result)
        {
            result = new ImageGenerationResult();

            if (!root.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
                return false;

            foreach (var candidate in candidates.EnumerateArray())
            {
                if (!candidate.TryGetProperty("content", out var content) ||
                    !content.TryGetProperty("parts", out var parts))
                {
                    continue;
                }

                string? revisedPrompt = null;
                foreach (var part in parts.EnumerateArray())
                {
                    if (part.TryGetProperty("text", out var textProp) && string.IsNullOrWhiteSpace(revisedPrompt))
                        revisedPrompt = textProp.GetString();

                    if (!TryGetInlineData(part, out var imageBytes, out var imageMimeType))
                        continue;

                    result = new ImageGenerationResult
                    {
                        IsSuccess = true,
                        ImageBytes = imageBytes,
                        ImageMimeType = imageMimeType ?? "image/png",
                        RevisedPrompt = revisedPrompt
                    };
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetInlineData(JsonElement part, out byte[] imageBytes, out string? imageMimeType)
        {
            imageBytes = Array.Empty<byte>();
            imageMimeType = null;

            if (!TryGetProperty(part, "inlineData", out var inlineData) &&
                !TryGetProperty(part, "inline_data", out inlineData))
            {
                return false;
            }

            if (!TryGetProperty(inlineData, "data", out var dataProp))
                return false;

            var base64 = dataProp.GetString();
            if (string.IsNullOrWhiteSpace(base64))
                return false;

            imageBytes = Convert.FromBase64String(base64);

            if (TryGetProperty(inlineData, "mimeType", out var mimeTypeProp) ||
                TryGetProperty(inlineData, "mime_type", out mimeTypeProp))
            {
                imageMimeType = mimeTypeProp.GetString();
            }

            return imageBytes.Length > 0;
        }

        private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }

            value = default;
            return false;
        }
    }
}
