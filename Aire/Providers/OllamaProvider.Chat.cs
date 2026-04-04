using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Aire.Providers
{
    public partial class OllamaProvider
    {
        /// <summary>
        /// Sends a non-streaming chat request to Ollama and converts any native tool call into Aire's tool-call text format.
        /// </summary>
        /// <param name="messages">Conversation history in Aire's provider-agnostic message format.</param>
        /// <param name="cancellationToken">Cancellation token for the HTTP request.</param>
        /// <returns>A normalized AI response containing plain text or a serialized tool call.</returns>
        public override async Task<AiResponse> SendChatAsync(
            IEnumerable<ChatMessage> messages,
            CancellationToken cancellationToken = default)
        {
            var startTime = DateTime.UtcNow;
            try
            {
                var msgList = messages.Select(ConvertToOllamaMessage).ToList();
                var request = new OllamaChatRequest
                {
                    Model = Config.Model,
                    Messages = msgList,
                    Stream = false,
                    Tools = _noToolsModels.Contains(Config.Model) ? null : _toolDefinitions,
                    Options = new OllamaOptions
                    {
                        Temperature = Config.Temperature,
                        NumPredict = Config.MaxTokens > 0 ? Config.MaxTokens : null
                    }
                };

                var json = JsonSerializer.Serialize(request, SerializeOpts);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.SendAsync(BuildRequest(HttpMethod.Post, $"{_baseUrl}/api/chat", content), cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var errorText = await response.Content.ReadAsStringAsync(cancellationToken);

                    if ((int)response.StatusCode == 400 &&
                        errorText.Contains("does not support tools", StringComparison.OrdinalIgnoreCase))
                    {
                        _noToolsModels.Add(Config.Model);
                        request.Tools = null;
                        var retryJson = JsonSerializer.Serialize(request, SerializeOpts);
                        var retryContent = new StringContent(retryJson, Encoding.UTF8, "application/json");
                        response = await _httpClient.SendAsync(BuildRequest(HttpMethod.Post, $"{_baseUrl}/api/chat", retryContent), cancellationToken);
                        if (!response.IsSuccessStatusCode)
                        {
                            errorText = await response.Content.ReadAsStringAsync(cancellationToken);
                            return Fail($"Ollama API error ({(int)response.StatusCode}): {errorText}", startTime);
                        }
                    }
                    else
                    {
                        return Fail($"Ollama API error ({(int)response.StatusCode}): {errorText}", startTime);
                    }
                }

                var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
                var ollamaResponse = JsonSerializer.Deserialize<OllamaChatResponse>(responseJson, DeserializeOpts);

                if (ollamaResponse == null)
                    return Fail("Failed to parse Ollama response", startTime);

                if (ollamaResponse.Message?.ToolCalls?.Count > 0)
                {
                    return new AiResponse
                    {
                        Content = BuildToolCallText(ollamaResponse.Message.ToolCalls[0]),
                        TokensUsed = ollamaResponse.EvalCount ?? 0,
                        Duration = DateTime.UtcNow - startTime,
                        IsSuccess = true
                    };
                }

                return new AiResponse
                {
                    Content = ollamaResponse.Message?.Content ?? string.Empty,
                    TokensUsed = ollamaResponse.EvalCount ?? 0,
                    Duration = DateTime.UtcNow - startTime,
                    IsSuccess = true
                };
            }
            catch (HttpRequestException ex)
            {
                    System.Diagnostics.Debug.WriteLine($"[WARN] [{GetType().Name}.SendChat] {ex.GetType().Name}");
                return Fail($"Network error while contacting Ollama. Make sure Ollama is running at {_baseUrl}.", startTime);
            }
            catch (TaskCanceledException)
            {
                return Fail($"Request timed out after {(int)_requestTimeout.TotalMinutes} minutes", startTime);
            }
            catch (Exception ex)
            {
                    System.Diagnostics.Debug.WriteLine($"[WARN] [{GetType().Name}.SendChat] {ex.GetType().Name}");
                return Fail("Ollama request failed.", startTime);
            }
        }

        /// <summary>
        /// Streams a chat response from Ollama and emits text chunks as they arrive.
        /// If Ollama returns a native tool call, the final chunk is converted into Aire's tool-call text format.
        /// </summary>
        /// <param name="messages">Conversation history in Aire's provider-agnostic message format.</param>
        /// <param name="cancellationToken">Cancellation token for the streaming request.</param>
        public override async IAsyncEnumerable<string> StreamChatAsync(
            IEnumerable<ChatMessage> messages,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var request = new OllamaChatRequest
            {
                Model = Config.Model,
                Messages = messages.Select(ConvertToOllamaMessage).ToList(),
                Stream = true,
                Tools = _noToolsModels.Contains(Config.Model) ? null : _toolDefinitions,
                Options = new OllamaOptions
                {
                    Temperature = Config.Temperature,
                    NumPredict = Config.MaxTokens > 0 ? Config.MaxTokens : null
                }
            };

            var json = JsonSerializer.Serialize(request, SerializeOpts);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(BuildRequest(HttpMethod.Post, $"{_baseUrl}/api/chat", content), cancellationToken);
            }
            catch
            {
                yield break;
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorText = await response.Content.ReadAsStringAsync(cancellationToken);
                if ((int)response.StatusCode == 400 &&
                    errorText.Contains("does not support tools", StringComparison.OrdinalIgnoreCase))
                {
                    _noToolsModels.Add(Config.Model);
                    request.Tools = null;
                    var retryJson = JsonSerializer.Serialize(request, SerializeOpts);
                    var retryContent = new StringContent(retryJson, Encoding.UTF8, "application/json");
                    try
                    {
                        response = await _httpClient.SendAsync(BuildRequest(HttpMethod.Post, $"{_baseUrl}/api/chat", retryContent), cancellationToken);
                    }
                    catch
                    {
                        yield break;
                    }
                }

                if (!response.IsSuccessStatusCode)
                    yield break;
            }

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);

            OllamaToolCall? capturedToolCall = null;

            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (line == null)
                    break;

                if (string.IsNullOrEmpty(line))
                    continue;

                OllamaStreamChunk? chunk = null;
                try
                {
                    chunk = JsonSerializer.Deserialize<OllamaStreamChunk>(line, DeserializeOpts);
                }
                catch (JsonException)
                {
                    continue;
                }

                if (chunk?.Message?.ToolCalls?.Count > 0)
                    capturedToolCall = chunk.Message.ToolCalls[0];

                if (chunk?.Message?.Content is { Length: > 0 } text)
                    yield return text;
            }

            if (capturedToolCall != null)
                yield return BuildToolCallContent(capturedToolCall);
        }

        /// <summary>
        /// Creates an <see cref="HttpRequestMessage"/> with the optional Bearer token attached.
        /// Ollama 0.4.0+ supports OLLAMA_API_KEY; if one is configured we must send it on every request.
        /// </summary>
        private HttpRequestMessage BuildRequest(HttpMethod method, string url, HttpContent? body = null)
        {
            var req = new HttpRequestMessage(method, url) { Content = body };
            if (!string.IsNullOrEmpty(_apiKey))
                req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);

            return req;
        }

        /// <summary>
        /// Converts Aire's chat message shape into the Ollama API message payload, including inline image bytes when present.
        /// </summary>
        /// <param name="message">Aire chat message.</param>
        /// <returns>Equivalent Ollama API message payload.</returns>
        private static OllamaMessage ConvertToOllamaMessage(ChatMessage message) => new()
        {
            Role = message.Role switch
            {
                "user" => "user",
                "assistant" => "assistant",
                "system" => "system",
                _ => "user"
            },
            Content = message.Content,
            Images = message.ImageBytes != null
                ? [Convert.ToBase64String(message.ImageBytes)]
                : null
        };

        /// <summary>
        /// Converts a native Ollama tool call into the &lt;tool_call&gt;JSON&lt;/tool_call&gt;
        /// format expected by ToolCallParser: {"tool":"name","param1":val,...}
        /// </summary>
        private static string BuildToolCallText(OllamaToolCall toolCall)
        {
            using var ms = new MemoryStream();
            using var writer = new Utf8JsonWriter(ms);
            writer.WriteStartObject();
            writer.WriteString("tool", toolCall.Function.Name);
            if (toolCall.Function.Arguments.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in toolCall.Function.Arguments.EnumerateObject())
                    prop.WriteTo(writer);
            }

            writer.WriteEndObject();
            writer.Flush();
            return $"<tool_call>{Encoding.UTF8.GetString(ms.ToArray())}</tool_call>";
        }

        /// <summary>
        /// Compatibility alias used by the streaming path when it needs the same tool-call serialization logic.
        /// </summary>
        private static string BuildToolCallContent(OllamaToolCall toolCall) => BuildToolCallText(toolCall);

        /// <summary>
        /// Builds a normalized failed provider response with elapsed time already filled in.
        /// </summary>
        private static AiResponse Fail(string message, DateTime startTime) => new()
        {
            IsSuccess = false,
            ErrorMessage = message,
            Duration = DateTime.UtcNow - startTime
        };
    }
}
