using System;
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

public sealed class AgentToolServiceTests : IDisposable
{
    private readonly string? _localAppDataBackup = Environment.GetEnvironmentVariable("LOCALAPPDATA");
    private readonly string _localAppDataOverride = Path.Combine(Path.GetTempPath(), "aire-agent-tool-tests", Guid.NewGuid().ToString("N"));

    public AgentToolServiceTests()
    {
        Directory.CreateDirectory(_localAppDataOverride);
        Environment.SetEnvironmentVariable("LOCALAPPDATA", _localAppDataOverride);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("LOCALAPPDATA", _localAppDataBackup);
        try { Directory.Delete(_localAppDataOverride, recursive: true); } catch { }
    }

    [Fact]
    public async Task ExecuteShowImageAsync_RejectsMissingPath()
    {
        var service = new AgentToolService();

        var result = await service.ExecuteShowImageAsync(CreateRequest(new { path_or_url = "" }));

        Assert.Equal("Error: path_or_url is required.", result.TextResult);
        Assert.Null(result.ScreenshotPath);
    }

    [Fact]
    public async Task ExecuteShowImageAsync_ReturnsLocalFilePath_WithDefaultCaption()
    {
        var service = new AgentToolService();
        var imagePath = Path.Combine(_localAppDataOverride, "local-image.png");
        await File.WriteAllBytesAsync(imagePath, [1, 2, 3, 4]);

        var result = await service.ExecuteShowImageAsync(CreateRequest(new { path_or_url = imagePath }));

        Assert.Equal("Showing: local-image.png", result.TextResult);
        Assert.Equal(imagePath, result.ScreenshotPath);
    }

    [Fact]
    public async Task ExecuteShowImageAsync_ReturnsCustomCaption_ForLocalFile()
    {
        var service = new AgentToolService();
        var imagePath = Path.Combine(_localAppDataOverride, "captioned-image.png");
        await File.WriteAllBytesAsync(imagePath, [1, 2, 3, 4]);

        var result = await service.ExecuteShowImageAsync(CreateRequest(new { path_or_url = imagePath, caption = "Shown in chat" }));

        Assert.Equal("Shown in chat", result.TextResult);
        Assert.Equal(imagePath, result.ScreenshotPath);
    }

    [Fact]
    public async Task ExecuteShowImageAsync_ReturnsNotFound_ForMissingLocalFile()
    {
        var service = new AgentToolService();
        var imagePath = Path.Combine(_localAppDataOverride, "missing-image.png");

        var result = await service.ExecuteShowImageAsync(CreateRequest(new { path_or_url = imagePath }));

        Assert.Contains("File not found", result.TextResult, StringComparison.OrdinalIgnoreCase);
        Assert.Null(result.ScreenshotPath);
    }

    [Fact]
    public async Task ExecuteShowImageAsync_DownloadsHttpImage_AndUsesContentTypeExtension()
    {
        using var server = await TestHttpServer.StartAsync(
            "image-bytes",
            statusCode: 200,
            contentType: "image/jpeg");
        var service = new AgentToolService();

        var result = await service.ExecuteShowImageAsync(CreateRequest(new { path_or_url = server.Url, caption = "Remote image" }));

        Assert.Equal("Remote image", result.TextResult);
        Assert.NotNull(result.ScreenshotPath);
        Assert.EndsWith(".jpg", result.ScreenshotPath, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(result.ScreenshotPath));
        Assert.Equal("image-bytes", await File.ReadAllTextAsync(result.ScreenshotPath));
    }

    [Fact]
    public async Task ExecuteShowImageAsync_UsesHttpStatusError_ForFailedDownload()
    {
        using var server = await TestHttpServer.StartAsync(
            "nope",
            statusCode: 404,
            contentType: "text/plain");
        var service = new AgentToolService();

        var result = await service.ExecuteShowImageAsync(CreateRequest(new { path_or_url = server.Url }));

        Assert.Equal("Error downloading image: HTTP 404", result.TextResult);
        Assert.Null(result.ScreenshotPath);
    }

    private static ToolCallRequest CreateRequest(object parameters)
    {
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(parameters));
        return new ToolCallRequest
        {
            Tool = "show_image",
            Parameters = doc.RootElement.Clone(),
            Description = "show image",
            RawJson = doc.RootElement.GetRawText()
        };
    }

    private sealed class TestHttpServer : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly Task _serveTask;
        private readonly string _body;
        private readonly int _statusCode;
        private readonly string _contentType;

        private TestHttpServer(TcpListener listener, string body, int statusCode, string contentType)
        {
            _listener = listener;
            _body = body;
            _statusCode = statusCode;
            _contentType = contentType;
            _serveTask = Task.Run(ServeOnceAsync);
        }

        public string Url { get; private init; } = string.Empty;

        public static Task<TestHttpServer> StartAsync(string body, int statusCode, string contentType)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            return Task.FromResult(new TestHttpServer(listener, body, statusCode, contentType)
            {
                Url = $"http://127.0.0.1:{port}/image"
            });
        }

        private async Task ServeOnceAsync()
        {
            using var client = await _listener.AcceptTcpClientAsync();
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, leaveOpen: true);
            while (true)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrEmpty(line))
                    break;
            }

            var bodyBytes = Encoding.UTF8.GetBytes(_body);
            var statusText = _statusCode == 200 ? "OK" : "Not Found";
            var header =
                $"HTTP/1.1 {_statusCode} {statusText}\r\n" +
                $"Content-Type: {_contentType}\r\n" +
                $"Content-Length: {bodyBytes.Length}\r\n" +
                "Connection: close\r\n\r\n";
            var headerBytes = Encoding.ASCII.GetBytes(header);

            await stream.WriteAsync(headerBytes);
            await stream.WriteAsync(bodyBytes);
            await stream.FlushAsync();
        }

        public void Dispose()
        {
            try { _listener.Stop(); } catch { }
            try { _serveTask.GetAwaiter().GetResult(); } catch { }
        }
    }
}
