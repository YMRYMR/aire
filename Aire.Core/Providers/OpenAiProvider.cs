using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Aire.Data;
using Aire.Domain.Providers;
using Aire.Services;
using OpenAI;
using OpenAI.Managers;
using OpenAI.ObjectModels;
using OpenAI.ObjectModels.RequestModels;
using OpenAI.ObjectModels.ResponseModels;
using OpenAI.ObjectModels.SharedModels;
using OpenAiChatMessage = OpenAI.ObjectModels.RequestModels.ChatMessage;

namespace Aire.Providers
{
    /// <summary>
    /// OpenAI provider implementation for OpenAI and OpenAI-compatible APIs.
    /// Compatible providers should inherit from this class and override only
    /// the metadata or provider-specific endpoints they need to customize.
    /// </summary>
    public class OpenAiProvider : BaseAiProvider, IImageGenerationProvider
    {
        protected OpenAIService? _openAiService;

        protected static readonly HttpClient MetadataHttp = new() { Timeout = TimeSpan.FromSeconds(8) };

        public override string ProviderType => "OpenAI";
        public override string DisplayName => "OpenAI (ChatGPT)";
        protected override ProviderCapabilities GetBaseCapabilities() =>
            ProviderCapabilities.TextChat |
            ProviderCapabilities.Streaming |
            ProviderCapabilities.ImageInput |
            ProviderCapabilities.ToolCalling |
            ProviderCapabilities.SystemPrompt;

        protected override ToolCallMode DefaultToolCallMode => ToolCallMode.NativeFunctionCalling;
        protected override ToolOutputFormat DefaultToolOutputFormat => ToolOutputFormat.NativeToolCalls;
        protected override bool PreferCompactToolDescriptions => true;

        protected virtual string DefaultApiBaseUrl => "https://api.openai.com";

        protected virtual string[] ModelIdPrefixes => new[] { "gpt-", "o1", "o3", "o4" };
        protected virtual bool SupportsImageGenerationOnCurrentModel =>
            Config.ModelCapabilities?.Contains("imagegeneration", StringComparer.OrdinalIgnoreCase) == true;

        protected virtual bool SupportsTokenUsageEndpoint(string baseUrl) =>
            baseUrl.Contains("api.openai.com", StringComparison.OrdinalIgnoreCase);

        protected virtual string BuildModelsUrl(string baseUrl) => $"{baseUrl}/v1/models";

        protected virtual string BuildChatCompletionsUrl(string baseUrl) => $"{baseUrl}/v1/chat/completions";

        protected virtual string BuildTokenUsageUrl(string baseUrl) => $"{baseUrl}/dashboard/billing/usage";
        protected virtual string BuildImageGenerationUrl(string baseUrl) => $"{baseUrl}/v1/images/generations";

        protected string EffectiveBaseUrl =>
            string.IsNullOrWhiteSpace(Config.BaseUrl)
                ? DefaultApiBaseUrl
                : Config.BaseUrl.TrimEnd('/');

        private static IEnumerable<ToolDescriptor> GetFilteredTools(IEnumerable<string>? capabilities, IEnumerable<string>? enabledCategories)
        {
            // Unknown capabilities → don't assume tool support; send no tools.
            if (capabilities == null)
                return Array.Empty<ToolDescriptor>();

            var caps = new HashSet<string>(capabilities, StringComparer.OrdinalIgnoreCase);
            bool hasTools = caps.Contains("tools") || caps.Contains("toolcalling");
            if (!hasTools) return Array.Empty<ToolDescriptor>();

            var allowedCategories = enabledCategories != null
                ? new HashSet<string>(enabledCategories, StringComparer.OrdinalIgnoreCase)
                : null;

            return SharedToolDefinitions.AllTools.Where(t =>
                (allowedCategories == null || allowedCategories.Contains(t.Category)) &&
                (t.Category != "mouse" || caps.Contains("mouse")) &&
                (t.Category != "keyboard" || caps.Contains("keyboard")) &&
                (t.Category != "system" || caps.Contains("system")) &&
                (t.Category != "email" || caps.Contains("email")));
        }

        private static PropertyDefinition CreatePropertyDefinition(ToolParam param)
        {
            var prop = new PropertyDefinition
            {
                Type = param.Type,
                Description = param.Description
            };
            if (param.Items != null)
            {
                prop.Items = CreatePropertyDefinition(param.Items);
            }
            return prop;
        }

        private static PropertyDefinition BuildRootSchema(ToolDescriptor tool)
        {
            var root = new PropertyDefinition
            {
                Type = "object",
                Properties = new Dictionary<string, PropertyDefinition>(),
                Required = tool.Required.ToList()
            };

            foreach (var (name, param) in tool.Parameters)
            {
                root.Properties[name] = CreatePropertyDefinition(param);
            }

            return root;
        }

        protected virtual IList<ToolDefinition> GetFunctionDefinitions()
        {
            var filteredTools = GetFilteredTools(Config.ModelCapabilities, Config.EnabledToolCategories);
            var compact = PreferCompactToolDescriptions;
            var tools = new List<ToolDefinition>();
            foreach (var tool in filteredTools)
            {
                var rootSchema = BuildRootSchema(tool);
                tools.Add(new ToolDefinition
                {
                    Type = "function",
                    Function = new FunctionDefinition
                    {
                        Name = tool.Name,
                        Description = tool.GetDescription(compact),
                        Parameters = rootSchema
                    }
                });
            }

            return tools;
        }

        public override async Task<List<ModelDefinition>?> FetchLiveModelsAsync(
            string? apiKey, string? baseUrl, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(apiKey)) return null;

            try
            {
                var url = string.IsNullOrWhiteSpace(baseUrl)
                    ? DefaultApiBaseUrl
                    : baseUrl.TrimEnd('/');

                using var req = new HttpRequestMessage(HttpMethod.Get, BuildModelsUrl(url));
                req.Headers.Add("Authorization", $"Bearer {apiKey}");
                using var res = await MetadataHttp.SendAsync(req, ct);
                if (!res.IsSuccessStatusCode) return null;

                var json = await res.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("data", out var dataEl)) return null;

                return dataEl.EnumerateArray()
                    .Select(m => m.GetProperty("id").GetString() ?? string.Empty)
                    .Where(id => !string.IsNullOrEmpty(id) &&
                                 ModelIdPrefixes.Any(p => id.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
                    .OrderByDescending(id => id)
                    .Select(id => new ModelDefinition { Id = id, DisplayName = id })
                    .ToList();
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"{GetType().Name}.FetchLiveModels", $"{ProviderType} live model fetch failed", ex);
                return null;
            }
        }

        public override async Task<TokenUsage?> GetTokenUsageAsync(CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(Config.ApiKey))
                return null;

            var baseUrl = EffectiveBaseUrl;
            if (!SupportsTokenUsageEndpoint(baseUrl))
                return null;

            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, BuildTokenUsageUrl(baseUrl));
                req.Headers.Add("Authorization", $"Bearer {Config.ApiKey}");
                using var response = await MetadataHttp.SendAsync(req, ct);
                if (!response.IsSuccessStatusCode)
                    return null;

                var json = await response.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (!root.TryGetProperty("total_usage", out var totalEl) ||
                    !root.TryGetProperty("hard_limit", out var limitEl))
                    return null;

                var totalCents = totalEl.GetInt64();
                var limitCents = limitEl.GetInt64();
                DateTime? resetDate = null;
                if (root.TryGetProperty("end_date", out var endDateEl) &&
                    DateTime.TryParse(endDateEl.GetString(), out var endDate))
                    resetDate = endDate;

                return new TokenUsage
                {
                    Used = totalCents,
                    Limit = limitCents,
                    ResetDate = resetDate,
                    Unit = "USD"
                };
            }
            catch
            {
                return null;
            }
        }

        public override IReadOnlyList<ProviderAction> Actions => new[]
        {
            new ProviderAction
            {
                Id = "refresh-models",
                Label = "Refresh Models",
                Placement = ProviderActionPlacement.ModelArea,
            },
        };

        public bool SupportsImageGeneration => SupportsImageGenerationOnCurrentModel;

        // The Betalgo SDK uses BaseDomain as a full URL and builds the request path as
        // "/{ApiVersion}/chat/completions".  Providers whose base URL contains a path
        // prefix (e.g. https://api.groq.com/openai) must split that prefix into
        // ApiVersion so the SDK constructs the correct endpoint.
        internal static (string Host, string ApiVersion) SplitSdkUrl(string url)
        {
            var uri = new Uri(url.TrimEnd('/'));
            var host = $"{uri.Scheme}://{uri.Host}"
                     + (uri.IsDefaultPort ? "" : $":{uri.Port}");
            var pathPrefix = uri.AbsolutePath.Trim('/');
            var apiVersion = string.IsNullOrEmpty(pathPrefix) ? "v1" : $"{pathPrefix}/v1";
            return (host, apiVersion);
        }

        public override void Initialize(ProviderConfig config)
        {
            base.Initialize(config);

            if (string.IsNullOrWhiteSpace(Config.ApiKey))
                throw new ArgumentException($"API key is required for {DisplayName} provider.");

            var baseUrl = !string.IsNullOrWhiteSpace(Config.BaseUrl)
                ? Config.BaseUrl.TrimEnd('/')
                : DefaultApiBaseUrl;

            var (host, apiVersion) = SplitSdkUrl(baseUrl);

            var options = new OpenAiOptions
            {
                ApiKey      = Config.ApiKey,
                BaseDomain  = host,
                ApiVersion  = apiVersion
            };

            _openAiService = new OpenAIService(options);
        }

        internal static string ConvertFunctionCallToToolCall(string functionName, string argumentsJson)
        {
            using var doc = JsonDocument.Parse(argumentsJson);
            var root = doc.RootElement;

            var toolCall = new Dictionary<string, object?>
            {
                ["tool"] = functionName
            };

            foreach (var prop in root.EnumerateObject())
            {
                toolCall[prop.Name] = prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString(),
                    JsonValueKind.Number => prop.Value.GetInt32(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Null => null,
                    _ => prop.Value.GetRawText()
                };
            }

            var json = JsonSerializer.Serialize(toolCall, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            return $"<tool_call>{json}</tool_call>";
        }

        public override async Task<AiResponse> SendChatAsync(
            IEnumerable<ChatMessage> messages, CancellationToken cancellationToken = default)
        {
            if (_openAiService == null)
                throw new InvalidOperationException("Provider not initialized.");

            var startTime = DateTime.UtcNow;
            try
            {
                var chatMessages = messages.Select(ConvertToOpenAiMessage).ToList();
                var request = new ChatCompletionCreateRequest
                {
                    Messages = chatMessages,
                    Model = Config.Model,
                    Temperature = (float?)Config.Temperature,
                    MaxTokens = EffectiveMaxTokens,
                    Stream = false
                };

                if (!Config.SkipNativeTools &&
                    ToolOutputFormat == ToolOutputFormat.NativeToolCalls && Has(ProviderCapabilities.ToolCalling))
                {
                    var tools = GetFunctionDefinitions();
                    if (tools.Count > 0)
                    {
                        request.Tools = tools;
                        request.ToolChoice = new ToolChoice { Type = "auto" }; // explicit — some providers (e.g. Groq) treat missing as "none"
                    }
                }

                var response = await _openAiService.ChatCompletion.CreateCompletion(
                    request, cancellationToken: cancellationToken);

                if (!response.Successful)
                {
                    return new AiResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = response.Error?.Message ?? $"Unknown error from {DisplayName} API",
                        Duration = DateTime.UtcNow - startTime
                    };
                }

                var firstChoice = response.Choices.FirstOrDefault();
                var content = firstChoice?.Message.Content ?? string.Empty;
                var tokensUsed = response.Usage?.TotalTokens ?? 0;

                var tc = firstChoice?.Message.ToolCalls?.FirstOrDefault();
                if (tc?.FunctionCall is { Name: { Length: > 0 } } fc)
                    content = ConvertFunctionCallToToolCall(fc.Name, fc.Arguments ?? "{}");
                else if (firstChoice?.Message.FunctionCall is { Name: { Length: > 0 } } legacyFc)
                    content = ConvertFunctionCallToToolCall(legacyFc.Name, legacyFc.Arguments ?? "{}");

                return new AiResponse
                {
                    Content = content,
                    TokensUsed = tokensUsed,
                    Duration = DateTime.UtcNow - startTime,
                    IsSuccess = true
                };
            }
            catch (HttpRequestException ex) when (ex.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
            {
                var hint = string.IsNullOrWhiteSpace(Config.BaseUrl) ? "Check your API key." : "Check your API key and base URL.";
                AppLogger.Warn($"{GetType().Name}.SendChat", "Auth error", ex);
                return new AiResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"Authentication failed. {hint}",
                    Duration = DateTime.UtcNow - startTime
                };
            }
            catch (HttpRequestException ex) when (ex.StatusCode is System.Net.HttpStatusCode.NotFound)
            {
                AppLogger.Warn($"{GetType().Name}.SendChat", "Model not found", ex);
                return new AiResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"Model \"{Config.Model}\" was not found. Check the model name in settings.",
                    Duration = DateTime.UtcNow - startTime
                };
            }
            catch (HttpRequestException ex) when (ex.InnerException is System.Net.Sockets.SocketException || ex.Message.Contains("connection", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("refused", StringComparison.OrdinalIgnoreCase))
            {
                AppLogger.Warn($"{GetType().Name}.SendChat", "Network error", ex);
                return new AiResponse
                {
                    IsSuccess = false,
                    ErrorMessage = ProviderErrorClassifier.SanitizeNetworkError(ex.Message, DisplayName),
                    Duration = DateTime.UtcNow - startTime
                };
            }
            catch (TaskCanceledException)
            {
                throw;
            }
            catch (HttpRequestException ex)
            {
                var readable = ProviderErrorClassifier.ExtractReadableMessage(ex.Message)
                    ?? ex.Message.Replace("\n", " ").Trim();
                if (readable.Length > 200) readable = readable[..200] + "…";
                AppLogger.Warn($"{GetType().Name}.SendChat", "Request failed", ex);
                return new AiResponse
                {
                    IsSuccess = false,
                    ErrorMessage = readable,
                    Duration = DateTime.UtcNow - startTime
                };
            }
            catch (Exception ex)
            {
                var readable = ProviderErrorClassifier.ExtractReadableMessage(ex.Message) ?? ex.Message;
                if (readable.Length > 200) readable = readable[..200] + "…";
                AppLogger.Warn($"{GetType().Name}.SendChat", "Request failed", ex);
                return new AiResponse
                {
                    IsSuccess = false,
                    ErrorMessage = string.IsNullOrWhiteSpace(readable) ? $"{DisplayName} request failed." : readable,
                    Duration = DateTime.UtcNow - startTime
                };
            }
        }

        public override async IAsyncEnumerable<string> StreamChatAsync(
            IEnumerable<ChatMessage> messages,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (_openAiService == null)
                throw new InvalidOperationException("Provider not initialized.");

            var chatMessages = messages.Select(ConvertToOpenAiMessage).ToList();
            var request = new ChatCompletionCreateRequest
            {
                Messages = chatMessages,
                Model = Config.Model,
                Temperature = (float?)Config.Temperature,
                MaxTokens = EffectiveMaxTokens,
                Stream = true
            };

            if (!Config.SkipNativeTools &&
                ToolOutputFormat == ToolOutputFormat.NativeToolCalls && Has(ProviderCapabilities.ToolCalling))
            {
                request.Tools = GetFunctionDefinitions();
            }

            var stream = _openAiService.ChatCompletion.CreateCompletionAsStream(
                request, cancellationToken: cancellationToken);
            await foreach (var chunk in stream.WithCancellation(cancellationToken))
            {
                if (!chunk.Successful)
                {
                    var error = chunk.Error?.Message;
                    if (!string.IsNullOrWhiteSpace(error))
                        yield return $"\n[Error: {ProviderErrorClassifier.ExtractReadableMessage(error) ?? error}]";
                    yield break;
                }

                var content = chunk.Choices.FirstOrDefault()?.Delta.Content;
                if (!string.IsNullOrEmpty(content))
                    yield return content;
            }
        }

        public override async Task<ProviderValidationResult> ValidateConfigurationAsync(CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(Config.ApiKey))
                return ProviderValidationResult.Fail("API key is required.");

            // Use a direct GET /v1/models call rather than sending a chat completion.
            // Sending a completion goes through the Betalgo SDK, which fails to deserialise
            // error responses where error.code is an integer instead of a string (common in
            // OpenRouter and other OpenAI-compatible providers), producing a confusing
            // JsonException instead of the real API error message.
            //
            // Some providers (e.g. MiMo) return 401 on /v1/models even though their chat
            // endpoint works fine (the key is valid; they just restrict model listing).
            // In that case fall back to a minimal chat probe — any non-auth response
            // (including billing errors) means the endpoint + key are correct.
            try
            {
                var url = BuildModelsUrl(EffectiveBaseUrl);
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Add("Authorization", $"Bearer {Config.ApiKey}");
                using var res = await MetadataHttp.SendAsync(req, cancellationToken);

                if (res.IsSuccessStatusCode)
                    return ProviderValidationResult.Ok();

                var body = await res.Content.ReadAsStringAsync(cancellationToken);

                // 401/403 on /models doesn't necessarily mean the key is wrong — some
                // compatible providers restrict model listing but accept chat requests.
                // Fall back to a lightweight chat probe to confirm the key actually works.
                if (res.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                    res.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    return await ValidateViaChatProbeAsync(cancellationToken);
                }

                var apiError = TryExtractApiErrorMessage(body);
                var message = apiError ?? $"HTTP {(int)res.StatusCode} {res.ReasonPhrase}";
                return ProviderValidationResult.Fail(message);
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"{GetType().Name}.ValidateConfiguration", "OpenAI configuration validation failed", ex);
                return ProviderValidationResult.Fail("OpenAI configuration validation failed.");
            }
        }

        /// <summary>
        /// Probes the chat completions endpoint with a minimal request to confirm the API key
        /// and base URL are correct when the models listing endpoint is unavailable.
        /// Any non-authentication response (including billing errors) is treated as valid config.
        /// </summary>
        private async Task<ProviderValidationResult> ValidateViaChatProbeAsync(CancellationToken cancellationToken)
        {
            try
            {
                var probeUrl = BuildChatCompletionsUrl(EffectiveBaseUrl);
                var payload = JsonSerializer.Serialize(new
                {
                    model = Config.Model,
                    max_tokens = 1,
                    messages = new[] { new { role = "user", content = "hi" } }
                });

                using var req = new HttpRequestMessage(HttpMethod.Post, probeUrl);
                req.Headers.Add("Authorization", $"Bearer {Config.ApiKey}");
                req.Content = new System.Net.Http.StringContent(payload, System.Text.Encoding.UTF8, "application/json");

                using var res = await MetadataHttp.SendAsync(req, cancellationToken);
                var body = await res.Content.ReadAsStringAsync(cancellationToken);

                if (res.IsSuccessStatusCode)
                    return ProviderValidationResult.Ok();

                // Auth errors mean the key is genuinely wrong.
                if (res.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                    res.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    var authError = TryExtractApiErrorMessage(body) ?? $"HTTP {(int)res.StatusCode} {res.ReasonPhrase}";
                    return ProviderValidationResult.Fail(authError);
                }

                // Any other error (billing, rate limits, bad model name, etc.) means the
                // endpoint and key are reachable — config is structurally valid.
                return ProviderValidationResult.Ok();
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"{GetType().Name}.ValidateViaChatProbe", "OpenAI chat probe validation failed", ex);
                return ProviderValidationResult.Fail("OpenAI configuration validation failed.");
            }
        }

        /// <summary>
        /// Extracts the human-readable message from an OpenAI-style error body without
        /// strict typing, so integer or null <c>error.code</c> values are tolerated.
        /// </summary>
        protected static string? TryExtractApiErrorMessage(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("error", out var error) &&
                    error.TryGetProperty("message", out var msg))
                    return msg.GetString();
            }
            catch { }
            return null;
        }

        protected virtual OpenAiChatMessage ConvertToOpenAiMessage(ChatMessage message)
        {
            var openAiMessage = new OpenAiChatMessage
            {
                Role = message.Role,
                Content = message.Content
            };

            if (message.ImageBytes != null && message.ImageBytes.Length > 0)
            {
                var mime = message.ImageMimeType ?? "image/png";
                var dataUrl = $"data:{mime};base64,{Convert.ToBase64String(message.ImageBytes)}";
                openAiMessage.Contents = new List<OpenAI.ObjectModels.RequestModels.MessageContent>
                {
                    OpenAI.ObjectModels.RequestModels.MessageContent.TextContent(message.Content ?? string.Empty),
                    OpenAI.ObjectModels.RequestModels.MessageContent.ImageUrlContent(dataUrl, "auto")
                };
                openAiMessage.Content = null;
            }

            return openAiMessage;
        }

        public virtual async Task<ImageGenerationResult> GenerateImageAsync(string prompt, CancellationToken cancellationToken = default)
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
                model = Config.Model,
                prompt,
                size = "1024x1024"
            });

            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, BuildImageGenerationUrl(EffectiveBaseUrl));
                req.Headers.Add("Authorization", $"Bearer {Config.ApiKey}");
                req.Content = new StringContent(payload, Encoding.UTF8, "application/json");

                using var response = await MetadataHttp.SendAsync(req, cancellationToken).ConfigureAwait(false);
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    return new ImageGenerationResult
                    {
                        IsSuccess = false,
                        ErrorMessage = TryExtractApiErrorMessage(body) ?? $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}"
                    };
                }

                using var doc = JsonDocument.Parse(body);
                if (!doc.RootElement.TryGetProperty("data", out var dataEl) || dataEl.GetArrayLength() == 0)
                {
                    return new ImageGenerationResult
                    {
                        IsSuccess = false,
                        ErrorMessage = "Image generation returned no image data."
                    };
                }

                var first = dataEl[0];
                string? revisedPrompt = first.TryGetProperty("revised_prompt", out var revisedPromptEl)
                    ? revisedPromptEl.GetString()
                    : null;

                if (first.TryGetProperty("b64_json", out var base64El) &&
                    !string.IsNullOrWhiteSpace(base64El.GetString()))
                {
                    return new ImageGenerationResult
                    {
                        IsSuccess = true,
                        ImageBytes = Convert.FromBase64String(base64El.GetString()!),
                        ImageMimeType = "image/png",
                        RevisedPrompt = revisedPrompt
                    };
                }

                if (first.TryGetProperty("url", out var urlEl) &&
                    Uri.TryCreate(urlEl.GetString(), UriKind.Absolute, out var imageUrl))
                {
                    var bytes = await MetadataHttp.GetByteArrayAsync(imageUrl, cancellationToken).ConfigureAwait(false);
                    return new ImageGenerationResult
                    {
                        IsSuccess = true,
                        ImageBytes = bytes,
                        ImageMimeType = GuessMimeType(imageUrl.AbsoluteUri) ?? "image/png",
                        RevisedPrompt = revisedPrompt
                    };
                }

                return new ImageGenerationResult
                {
                    IsSuccess = false,
                    ErrorMessage = "Image generation returned an unsupported payload."
                };
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"{GetType().Name}.GenerateImage", "OpenAI image generation failed", ex);
                return new ImageGenerationResult
                {
                    IsSuccess = false,
                    ErrorMessage = "OpenAI image generation failed."
                };
            }
        }
    }
}
