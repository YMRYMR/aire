using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Aire.Providers;
using Aire.Tests.Infrastructure;
using Xunit;

namespace Aire.Tests.Providers;

public sealed class OllamaProviderTests
{
    [Fact]
    public async Task SendChatAsync_ReturnsAssistantTextAndTokens()
    {
        using var server = new SimpleJsonServer((_, _, _) =>
            SimpleJsonServer.Json(200, """{"message":{"content":"Hello from Ollama"},"eval_count":42}"""));

        var provider = CreateProvider(server.BaseUrl);

        var response = await provider.SendChatAsync([new ChatMessage { Role = "user", Content = "Hello" }], CancellationToken.None);

        Assert.True(response.IsSuccess);
        Assert.Equal("Hello from Ollama", response.Content);
        Assert.Equal(42, response.TokensUsed);
        Assert.Single(server.RequestBodies);
        Assert.Contains("\"tools\"", server.RequestBodies[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendChatAsync_ConvertsNativeToolCallToAireToolCallText()
    {
        using var server = new SimpleJsonServer((_, _, _) =>
            SimpleJsonServer.Json(200,
                """{"message":{"tool_calls":[{"function":{"name":"read_file","arguments":{"path":"C:\\repo\\file.txt"}}}]}}"""));

        var provider = CreateProvider(server.BaseUrl);

        var response = await provider.SendChatAsync([new ChatMessage { Role = "user", Content = "Read the file." }], CancellationToken.None);

        Assert.True(response.IsSuccess);
        Assert.Contains("<tool_call>", response.Content, StringComparison.Ordinal);
        Assert.Contains("\"tool\":\"read_file\"", response.Content, StringComparison.Ordinal);
        Assert.Contains("C:\\\\repo\\\\file.txt", response.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendChatAsync_RetriesWithoutTools_WhenModelRejectsTools()
    {
        var callCount = 0;
        using var server = new SimpleJsonServer((_, _, body) =>
        {
            callCount++;
            return callCount == 1
                ? SimpleJsonServer.Text(400, "does not support tools")
                : SimpleJsonServer.Json(200, """{"message":{"content":"Retried"},"eval_count":7}""");
        });

        var provider = CreateProvider(server.BaseUrl);

        var response = await provider.SendChatAsync([new ChatMessage { Role = "user", Content = "Hello" }], CancellationToken.None);

        Assert.True(response.IsSuccess);
        Assert.Equal("Retried", response.Content);
        Assert.Equal(2, server.RequestBodies.Count);
        Assert.Contains("\"tools\"", server.RequestBodies[0], StringComparison.Ordinal);
        Assert.DoesNotContain("\"tools\"", server.RequestBodies[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendChatAsync_ReturnsGenericError_WhenTransportThrows()
    {
        var baseUrl = CreateUnreachableBaseUrl();
        var provider = CreateProvider(baseUrl);

        var response = await provider.SendChatAsync([new ChatMessage { Role = "user", Content = "Hello" }], CancellationToken.None);

        Assert.False(response.IsSuccess);
        Assert.Equal($"Network error while contacting Ollama. Make sure Ollama is running at {baseUrl}.", response.ErrorMessage);
    }

    [Fact]
    public async Task StreamChatAsync_YieldsTextAndFinalToolCall()
    {
        using var server = new SimpleJsonServer((_, _, _) =>
            SimpleJsonServer.Lines(200,
            [
                """{"message":{"content":"Hello "},"done":false}""",
                """{"message":{"tool_calls":[{"function":{"name":"list_directory","arguments":{"path":"C:\\repo"}}}]},"done":true}"""
            ]));

        var provider = CreateProvider(server.BaseUrl);
        var chunks = new List<string>();

        await foreach (var chunk in provider.StreamChatAsync([new ChatMessage { Role = "user", Content = "List the repo." }], CancellationToken.None))
            chunks.Add(chunk);

        Assert.Equal("Hello ", chunks[0]);
        Assert.Contains("<tool_call>", chunks[1], StringComparison.Ordinal);
        Assert.Contains("\"tool\":\"list_directory\"", chunks[1], StringComparison.Ordinal);
        Assert.Contains("C:\\\\repo", chunks[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidateConfigurationAsync_ReturnsInvalidWhenModelIsMissing()
    {
        using var server = new SimpleJsonServer((method, path, _) =>
        {
            if (method == "GET" && path == "/api/tags")
                return SimpleJsonServer.Json(200, """{"models":[{"name":"llama3.2:latest","size":123}]}""");

            return SimpleJsonServer.Json(404, """{"error":"missing"}""");
        });

        var provider = CreateProvider(server.BaseUrl);

        var validation = await provider.ValidateConfigurationAsync(CancellationToken.None);

        Assert.False(validation.IsValid);
        Assert.Contains("not found", validation.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateConfigurationAsync_ReturnsGenericError_WhenTransportThrows()
    {
        var provider = CreateProvider(CreateUnreachableBaseUrl());

        var validation = await provider.ValidateConfigurationAsync(CancellationToken.None);

        Assert.False(validation.IsValid);
        Assert.Equal("Ollama connection failed.", validation.Error);
    }

    [Fact]
    public async Task FetchLiveModelsAsync_NormalizesLatestTagAndMarksInstalledModels()
    {
        using var server = new SimpleJsonServer((method, path, _) =>
        {
            if (method == "GET" && path == "/api/tags")
            {
                return SimpleJsonServer.Json(200,
                    """{"models":[{"name":"qwen2.5-coder:latest","size":123456},{"name":"phi4:14b","size":456789}]}""");
            }

            return SimpleJsonServer.Json(404, """{"error":"missing"}""");
        });

        var provider = new OllamaProvider();

        var models = await provider.FetchLiveModelsAsync(null, server.BaseUrl, CancellationToken.None);

        Assert.NotNull(models);
        Assert.Contains(models!, m => m.Id == "qwen2.5-coder" && m.IsInstalled && m.SizeBytes == 123456);
        Assert.Contains(models!, m => m.Id == "phi4:14b" && m.IsInstalled && m.SizeBytes == 456789);
        Assert.DoesNotContain(models!, m => m.Id == "qwen2.5-coder:latest");
    }

    private static OllamaProvider CreateProvider(string baseUrl)
    {
        var provider = new OllamaProvider();
        provider.Initialize(new ProviderConfig
        {
            BaseUrl = baseUrl,
            Model = "qwen2.5-coder:7b",
            Temperature = 0.2,
            MaxTokens = 256,
            ModelCapabilities = ["tools"]
        });
        return provider;
    }

    private static string CreateUnreachableBaseUrl()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return $"http://127.0.0.1:{port}";
    }
}
