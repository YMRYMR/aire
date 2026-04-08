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

public class ZaiProviderTests
{
    [Fact]
    public async Task GenerateImageAsync_UsesDocumentedPaaSEndpoint_ForCodingBaseUrl()
    {
        string? baseUrl = null;
        using var server = new ZaiTestServer((method, path, _) =>
        {
            if (method == "POST" && path == "/api/paas/v4/images/generations")
            {
                return ZaiTestServer.Json(200,
                    """
                    {
                      "data": [
                        {
                          "url": "__IMAGE_URL__"
                        }
                      ]
                    }
                    """.Replace("__IMAGE_URL__", $"{baseUrl}/fake-image.png", System.StringComparison.Ordinal));
            }

            if (method == "GET" && path == "/fake-image.png")
                return new ZaiTestServer.Response(200, "image/png", new byte[] { 1, 2, 3, 4 });

            return ZaiTestServer.Json(404, """{"error":{"message":"missing"}}""");
        });
        baseUrl = server.BaseUrl;

        var provider = new ZaiProvider();
        provider.Initialize(new ProviderConfig
        {
            ApiKey = "zai-test",
            BaseUrl = $"{server.BaseUrl}/api/coding/paas/v4",
            Model = "glm-image",
            ModelCapabilities = new List<string> { "imagegeneration" }
        });

        var result = await provider.GenerateImageAsync("Create a glowing city", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, result.ImageBytes);
        Assert.Equal("image/png", result.ImageMimeType);
        Assert.True(provider.SupportsImageGeneration);
    }

    [Fact]
    public void NormalizeZaiError_ReturnsStableFallback_ForUnknownMessages()
    {
        const string raw = "OpenAI API returned 429 with host api.openai.com and stack trace details";

        var normalized = ZaiProvider.NormalizeZaiError(raw);

        Assert.Equal("An unexpected error occurred while processing your request through z.ai.", normalized);
        Assert.DoesNotContain("api.openai.com", normalized, System.StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("429", normalized, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NormalizeZaiError_PreservesReadableApiMessage()
    {
        const string raw = """
        {"error":{"message":"Model glm-5.1 is temporarily unavailable."}}
        """;

        var normalized = ZaiProvider.NormalizeZaiError(raw);

        Assert.Equal("Model glm-5.1 is temporarily unavailable.", normalized);
    }

    private sealed class ZaiTestServer : System.IDisposable
    {
        private readonly TcpListener _listener;
        private readonly Func<string, string, string, Response> _handler;
        private readonly Task _serveLoop;

        public ZaiTestServer(Func<string, string, string, Response> handler)
        {
            _handler = handler;
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            var port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            BaseUrl = $"http://127.0.0.1:{port}";
            _serveLoop = Task.Run(ServeAsync);
        }

        public string BaseUrl { get; }

        public static Response Json(int statusCode, string json) =>
            new(statusCode, "application/json", Encoding.UTF8.GetBytes(json));

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

                    int contentLength = 0;
                    string? line;
                    while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync()))
                    {
                        if (line.StartsWith("Content-Length:", System.StringComparison.OrdinalIgnoreCase))
                            contentLength = int.Parse(line[15..].Trim(), System.Globalization.CultureInfo.InvariantCulture);
                    }

                    string body = string.Empty;
                    if (contentLength > 0)
                    {
                        var buffer = new char[contentLength];
                        var read = 0;
                        while (read < contentLength)
                        {
                            var n = await reader.ReadAsync(buffer, read, contentLength - read);
                            if (n == 0) break;
                            read += n;
                        }

                        body = new string(buffer, 0, read);
                    }

                    var response = _handler(method, path, body);
                    await WriteResponseAsync(stream, response);
                }
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private static async Task WriteResponseAsync(NetworkStream stream, Response response)
        {
            var header = $"HTTP/1.1 {response.StatusCode} OK\r\nContent-Type: {response.ContentType}\r\nContent-Length: {response.Body.Length}\r\nConnection: close\r\n\r\n";
            await stream.WriteAsync(Encoding.ASCII.GetBytes(header));
            await stream.WriteAsync(response.Body);
        }

        public void Dispose()
        {
            _listener.Stop();
            try
            {
                _serveLoop.Wait(1000);
            }
            catch
            {
            }
        }

        public readonly record struct Response(int StatusCode, string ContentType, byte[] Body);
    }
}
