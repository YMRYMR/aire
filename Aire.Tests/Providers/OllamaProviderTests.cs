using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Aire.Providers;
using Xunit;

namespace Aire.Tests.Providers;

public sealed class OllamaProviderTests
{
    [Fact]
    public async Task SendChatAsync_ReturnsAssistantTextAndTokens()
    {
        using var server = new OllamaTestServer((_, _, _) =>
            OllamaTestServer.Json(200, """{"message":{"content":"Hello from Ollama"},"eval_count":42}"""));

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
        using var server = new OllamaTestServer((_, _, _) =>
            OllamaTestServer.Json(200,
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
        using var server = new OllamaTestServer((_, _, body) =>
        {
            callCount++;
            return callCount == 1
                ? OllamaTestServer.Text(400, "does not support tools")
                : OllamaTestServer.Json(200, """{"message":{"content":"Retried"},"eval_count":7}""");
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
        using var server = new OllamaTestServer((_, _, _) =>
            OllamaTestServer.Lines(200,
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
        using var server = new OllamaTestServer((method, path, _) =>
        {
            if (method == "GET" && path == "/api/tags")
                return OllamaTestServer.Json(200, """{"models":[{"name":"llama3.2:latest","size":123}]}""");

            return OllamaTestServer.Json(404, """{"error":"missing"}""");
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
        using var server = new OllamaTestServer((method, path, _) =>
        {
            if (method == "GET" && path == "/api/tags")
            {
                return OllamaTestServer.Json(200,
                    """{"models":[{"name":"qwen2.5-coder:latest","size":123456},{"name":"phi4:14b","size":456789}]}""");
            }

            return OllamaTestServer.Json(404, """{"error":"missing"}""");
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

    private sealed class OllamaTestServer : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly Func<string, string, string, Response> _handler;
        private readonly Task _serveLoop;

        public OllamaTestServer(Func<string, string, string, Response> handler)
        {
            _handler = handler;
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            var port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            BaseUrl = $"http://127.0.0.1:{port}";
            _serveLoop = Task.Run(ServeAsync);
        }

        public string BaseUrl { get; }
        public List<string> RequestBodies { get; } = [];

        public static Response Json(int statusCode, string json) =>
            new(statusCode, "application/json", Encoding.UTF8.GetBytes(json));

        public static Response Text(int statusCode, string text) =>
            new(statusCode, "text/plain", Encoding.UTF8.GetBytes(text));

        public static Response Lines(int statusCode, IEnumerable<string> lines) =>
            new(statusCode, "application/x-ndjson", Encoding.UTF8.GetBytes(string.Join("\n", lines) + "\n"));

        private async Task ServeAsync()
        {
            try
            {
                while (true)
                {
                    using var client = await _listener.AcceptTcpClientAsync();
                    using var stream = client.GetStream();
                    using var reader = new StreamReader(stream, leaveOpen: true);

                    var requestLine = await reader.ReadLineAsync();
                    if (requestLine == null)
                        continue;

                    var parts = requestLine.Split(' ');
                    var method = parts[0];
                    var path = parts[1];
                    var contentLength = 0;

                    string? line;
                    while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync()))
                    {
                        if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                            contentLength = int.Parse(line[15..].Trim());
                    }

                    var body = string.Empty;
                    if (contentLength > 0)
                    {
                        var buffer = new char[contentLength];
                        var read = 0;
                        while (read < contentLength)
                            read += await reader.ReadAsync(buffer, read, contentLength - read);
                        body = new string(buffer);
                    }

                    RequestBodies.Add(body);

                    var response = _handler(method, path, body);
                    var statusText = response.StatusCode == 200 ? "OK" : "Error";
                    var header =
                        $"HTTP/1.1 {response.StatusCode} {statusText}\r\n" +
                        $"Content-Type: {response.ContentType}\r\n" +
                        $"Content-Length: {response.Body.Length}\r\n" +
                        "Connection: close\r\n\r\n";

                    await stream.WriteAsync(Encoding.ASCII.GetBytes(header));
                    await stream.WriteAsync(response.Body);
                    await stream.FlushAsync();
                }
            }
            catch
            {
            }
        }

        public void Dispose()
        {
            try { _listener.Stop(); } catch { }
            try { _serveLoop.Wait(1000); } catch { }
        }

        public sealed record Response(int StatusCode, string ContentType, byte[] Body);
    }
}
