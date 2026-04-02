using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Aire.Providers;
using Xunit;

namespace Aire.Tests.Providers;

public class InceptionProviderTests
{
    [Fact]
    public void Initialize_WithoutApiKey_ThrowsArgumentException()
    {
        InceptionProvider provider = new InceptionProvider();
        ProviderConfig config = new ProviderConfig
        {
            ApiKey = "",
            Model = "mercury-latest"
        };
        Assert.Throws<ArgumentException>(delegate
        {
            provider.Initialize(config);
        });
    }

    [Fact]
    public void Initialize_WithApiKey_DoesNotThrow()
    {
        InceptionProvider provider = new InceptionProvider();
        ProviderConfig config = new ProviderConfig
        {
            ApiKey = "sk-test-key",
            Model = "mercury-latest",
            BaseUrl = "https://api.inceptionlabs.ai"
        };
        Exception ex = Record.Exception(delegate
        {
            provider.Initialize(config);
        });
        Assert.Null(ex);
    }

    [Fact]
    public void ProviderType_IsInception()
    {
        InceptionProvider inceptionProvider = new InceptionProvider();
        Assert.Equal("Inception", inceptionProvider.ProviderType);
    }

    [Fact]
    public void DisplayName_ContainsInception()
    {
        InceptionProvider inceptionProvider = new InceptionProvider();
        Assert.Contains("Inception", inceptionProvider.DisplayName, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SupportsImages_IsFalseByDefault()
    {
        InceptionProvider inceptionProvider = new InceptionProvider();
        Assert.False(inceptionProvider.SupportsImages);
    }

    [Fact]
    public void FieldHints_ShowBaseUrl_IsFalse()
    {
        InceptionProvider inceptionProvider = new InceptionProvider();

        Assert.False(inceptionProvider.FieldHints.ShowBaseUrl);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task FetchLiveModelsAsync_EmptyApiKey_ReturnsNull(string? apiKey)
    {
        InceptionProvider provider = new InceptionProvider();

        Assert.Null(await provider.FetchLiveModelsAsync(apiKey, null, CancellationToken.None));
    }

    [Fact]
    public async Task FetchLiveModelsAsync_FiltersAndSortsInceptionModels()
    {
        using var server = new SimpleJsonServer((method, path) =>
            method == "GET" && path == "/v1/models"
                ? SimpleJsonServer.Json(200, """{"data":[{"id":"mercury-coder"},{"id":"gpt-other"},{"id":"inception-fast"}]}""")
                : SimpleJsonServer.Json(404, """{"error":"missing"}"""));

        InceptionProvider provider = new InceptionProvider();

        var models = await provider.FetchLiveModelsAsync("inception-key", server.BaseUrl, CancellationToken.None);

        Assert.NotNull(models);
        Assert.Equal(new[] { "mercury-coder", "inception-fast" }, models!.Select(m => m.Id).ToArray());
    }

    [Fact]
    public async Task FetchLiveModelsAsync_MissingData_ReturnsNull()
    {
        using var server = new SimpleJsonServer((method, path) =>
            method == "GET" && path == "/v1/models"
                ? SimpleJsonServer.Json(200, """{"object":"list"}""")
                : SimpleJsonServer.Json(404, """{"error":"missing"}"""));

        InceptionProvider provider = new InceptionProvider();

        Assert.Null(await provider.FetchLiveModelsAsync("inception-key", server.BaseUrl, CancellationToken.None));
    }

    private sealed class SimpleJsonServer : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly Func<string, string, Response> _handler;
        private readonly Task _serveLoop;

        public SimpleJsonServer(Func<string, string, Response> handler)
        {
            _handler = handler;
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            BaseUrl = $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}";
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
                    if (requestLine == null) continue;
                    var parts = requestLine.Split(' ');
                    var response = _handler(parts[0], parts[1]);
                    while (!string.IsNullOrEmpty(await reader.ReadLineAsync())) { }
                    var header = $"HTTP/1.1 {response.StatusCode} {(response.StatusCode == 200 ? "OK" : "Error")}\r\nContent-Type: {response.ContentType}\r\nContent-Length: {response.Body.Length}\r\nConnection: close\r\n\r\n";
                    await stream.WriteAsync(Encoding.ASCII.GetBytes(header));
                    await stream.WriteAsync(response.Body);
                    await stream.FlushAsync();
                }
            }
            catch { }
        }

        public void Dispose()
        {
            try { _listener.Stop(); } catch { }
            try { _serveLoop.Wait(1000); } catch { }
        }

        public sealed record Response(int StatusCode, string ContentType, byte[] Body);
    }
}
