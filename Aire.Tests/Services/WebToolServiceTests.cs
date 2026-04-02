using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Aire.Services;
using Aire.Services.Tools;
using Xunit;

namespace Aire.Tests.Services;

public sealed class WebToolServiceTests
{
    [Fact]
    public async Task ExecuteOpenUrlAsync_RequiresUrl()
    {
        var service = new WebToolService(new WebFetchService());

        var result = await service.ExecuteOpenUrlAsync(new ToolCallRequest
        {
            Parameters = JsonDocument.Parse("{}").RootElement.Clone()
        });

        Assert.Contains("url parameter is required", result.TextResult, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteOpenUrlAsync_FetchesHtmlAndHonorsMaxCharsClamp()
    {
        var body = "<html><head><title>Example</title></head><body><p>" + new string('x', 1200) + "</p></body></html>";
        using var server = new TestHttpServer((_, _, _, _) => TestHttpServer.Html(200, body));
        using var fetch = new WebFetchService();
        var service = new WebToolService(fetch);

        var result = await service.ExecuteOpenUrlAsync(new ToolCallRequest
        {
            Parameters = JsonDocument.Parse($$"""{"url":"{{server.BaseUrl}}/page","max_chars":"100"}""").RootElement.Clone()
        });

        Assert.Contains($"URL: {server.BaseUrl}/page", result.TextResult, StringComparison.Ordinal);
        Assert.Contains("Title: Example", result.TextResult, StringComparison.Ordinal);
        Assert.Contains("truncated to 500 characters", result.TextResult, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteHttpRequestAsync_SendsCustomMethodHeadersAndTruncatesBody()
    {
        string? seenMethod = null;
        string? seenHeader = null;
        string? seenBody = null;
        using var server = new TestHttpServer((method, _, requestBody, headers) =>
        {
            seenMethod = method;
            headers.TryGetValue("X-Test", out seenHeader);
            seenBody = requestBody;
            return TestHttpServer.Text(202, new string('a', 21000));
        });

        var service = new WebToolService(new WebFetchService());
        using var requestDoc = JsonDocument.Parse(
            $$"""{"url":"{{server.BaseUrl}}/api","method":"post","headers":"{\"X-Test\":\"yes\"}","body":"{\"hello\":true}"}""");

        var result = await service.ExecuteHttpRequestAsync(new ToolCallRequest
        {
            Parameters = requestDoc.RootElement.Clone()
        });

        Assert.Equal("POST", seenMethod);
        Assert.Equal("yes", seenHeader);
        Assert.Equal("{\"hello\":true}", seenBody);
        Assert.Contains("HTTP 202", result.TextResult, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[...truncated...]", result.TextResult, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteHttpRequestAsync_IgnoresMalformedHeaders_AndMissingSchemeFailsGracefully()
    {
        string? seenPath = null;
        using var server = new TestHttpServer((method, path, _, _) =>
        {
            seenPath = path;
            return TestHttpServer.Text(200, "ok");
        });

        var service = new WebToolService(new WebFetchService());
        using var validRequestDoc = JsonDocument.Parse(
            $$"""{"url":"{{server.BaseUrl}}/hello","method":"get","headers":"not-json"}""");

        var success = await service.ExecuteHttpRequestAsync(new ToolCallRequest
        {
            Parameters = validRequestDoc.RootElement.Clone()
        });

        Assert.Equal("/hello", seenPath);
        Assert.Contains("HTTP 200", success.TextResult, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ok", success.TextResult, StringComparison.Ordinal);

        using var missingSchemeDoc = JsonDocument.Parse(
            $$"""{"url":"127.0.0.1:{{new Uri(server.BaseUrl).Port}}/hello","method":"get"}""");

        var failure = await service.ExecuteHttpRequestAsync(new ToolCallRequest
        {
            Parameters = missingSchemeDoc.RootElement.Clone()
        });

        Assert.Contains("Error:", failure.TextResult, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class TestHttpServer : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly Func<string, string, string, Dictionary<string, string>, Response> _handler;
        private readonly Task _serveLoop;

        public TestHttpServer(Func<string, string, string, Dictionary<string, string>, Response> handler)
        {
            _handler = handler;
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            var port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            BaseUrl = $"http://127.0.0.1:{port}";
            _serveLoop = Task.Run(ServeAsync);
        }

        public string BaseUrl { get; }

        public static Response Html(int statusCode, string html) =>
            new(statusCode, "text/html", Encoding.UTF8.GetBytes(html));

        public static Response Text(int statusCode, string text) =>
            new(statusCode, "text/plain", Encoding.UTF8.GetBytes(text));

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
                    var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                    string? line;
                    while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync()))
                    {
                        var separator = line.IndexOf(':');
                        if (separator > 0)
                        {
                            var key = line[..separator].Trim();
                            var value = line[(separator + 1)..].Trim();
                            headers[key] = value;
                        }

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

                    var response = _handler(method, path, body, headers);
                    var statusText = response.StatusCode == 200 ? "OK" : "Custom";
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
