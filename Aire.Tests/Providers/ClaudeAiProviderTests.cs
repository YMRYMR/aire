using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Aire.Providers;
using Xunit;

namespace Aire.Tests.Providers;

public sealed class ClaudeAiProviderTests
{
    [Fact]
    public async Task StreamChatAsync_EmitsChunks_AndBuildsSystemAndImagePayload()
    {
        var handler = new RecordingClaudeHandler(request =>
        {
            if (request.Method == HttpMethod.Post && request.RequestUri?.AbsolutePath == "/v1/messages")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """
                        data: {"type":"content_block_delta","delta":{"text":"Hello "}}
                        data: {"type":"content_block_delta","delta":{"text":"Claude"}}
                        data: [DONE]

                        """,
                        Encoding.UTF8,
                        "text/event-stream")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("""{"error":"missing"}""", Encoding.UTF8, "application/json")
            };
        });

        var provider = CreateProvider(handler);
        var chunks = new List<string>();

        await foreach (var chunk in provider.StreamChatAsync(
            [
                new ChatMessage { Role = "system", Content = "Follow the rules." },
                new ChatMessage { Role = "user", Content = "Describe this", ImageBytes = [1, 2, 3], ImageMimeType = "image/png" }
            ],
            CancellationToken.None))
        {
            chunks.Add(chunk);
        }

        Assert.Equal(["Hello ", "Claude"], chunks);
        Assert.NotNull(handler.LastBody);
        Assert.Contains("\"system\":[{\"type\":\"text\",\"text\":\"Follow the rules.\"", handler.LastBody, StringComparison.Ordinal);
        Assert.Contains("\"cache_control\":{\"type\":\"ephemeral\"}", handler.LastBody, StringComparison.Ordinal);
        Assert.Contains("\"media_type\":\"image/png\"", handler.LastBody, StringComparison.Ordinal);
        Assert.Contains("\"data\":\"AQID\"", handler.LastBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidateConfigurationAsync_ReturnsOk_WhenModelsEndpointSucceeds()
    {
        var handler = new RecordingClaudeHandler(request =>
        {
            if (request.Method == HttpMethod.Get && request.RequestUri?.AbsolutePath == "/v1/models")
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}", Encoding.UTF8, "application/json") };

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var provider = CreateProvider(handler);

        var validation = await provider.ValidateConfigurationAsync(CancellationToken.None);

        Assert.True(validation.IsValid);
    }

    [Fact]
    public async Task SendChatAsync_ReturnsFailure_WhenAnthropicApiReturnsError()
    {
        var handler = new RecordingClaudeHandler(request =>
        {
            if (request.Method == HttpMethod.Post && request.RequestUri?.AbsolutePath == "/v1/messages")
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("""{"error":"boom"}""", Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var provider = CreateProvider(handler);

        var response = await provider.SendChatAsync([new ChatMessage { Role = "user", Content = "Hello" }], CancellationToken.None);

        Assert.False(response.IsSuccess);
        Assert.Equal("Anthropic request failed.", response.ErrorMessage);
    }

    [Fact]
    public async Task SendChatAsync_ReturnsSanitizedError_OnHttpException()
    {
        var handler = new RecordingClaudeHandler(request =>
            throw new HttpRequestException("secret://internal-host"));

        var provider = CreateProvider(handler);

        var response = await provider.SendChatAsync([new ChatMessage { Role = "user", Content = "Hello" }], CancellationToken.None);

        Assert.False(response.IsSuccess);
        Assert.Equal("Anthropic request failed.", response.ErrorMessage);
        Assert.DoesNotContain("internal-host", response.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateConfigurationAsync_ReturnsSanitizedError_OnHttpException()
    {
        var handler = new RecordingClaudeHandler(request =>
            throw new HttpRequestException("secret://internal-host"));

        var provider = CreateProvider(handler);

        var validation = await provider.ValidateConfigurationAsync(CancellationToken.None);

        Assert.False(validation.IsValid);
        Assert.Equal("Anthropic configuration validation failed.", validation.Error);
        Assert.DoesNotContain("internal-host", validation.Error, StringComparison.OrdinalIgnoreCase);
    }

    private static ClaudeAiProvider CreateProvider(RecordingClaudeHandler handler)
    {
        var provider = new ClaudeAiProvider();
        provider.Initialize(new ProviderConfig
        {
            ApiKey = "claude-test-key",
            Model = "claude-sonnet-4-5",
            MaxTokens = 256,
            TimeoutMinutes = 2
        });

        var httpField = typeof(ClaudeAiProvider).GetField("_http", BindingFlags.Instance | BindingFlags.NonPublic)!;
        httpField.SetValue(provider, new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) });
        return provider;
    }

    private sealed class RecordingClaudeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public RecordingClaudeHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastBody = request.Content == null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return _handler(request);
        }
    }
}
