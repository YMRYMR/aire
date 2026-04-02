using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Aire.Providers;
using Xunit;

namespace Aire.Tests.Providers;

public class GroqProviderTests
{
    [Fact]
    public void ProviderType_IsGroq()
    {
        GroqProvider groqProvider = new GroqProvider();
        Assert.Equal("Groq", groqProvider.ProviderType);
    }

    [Fact]
    public void DisplayName_IsGroq()
    {
        GroqProvider groqProvider = new GroqProvider();
        Assert.Equal("Groq", groqProvider.DisplayName);
    }

    [Fact]
    public void FieldHints_ShowBaseUrl_IsFalse()
    {
        GroqProvider groqProvider = new GroqProvider();
        Assert.False(groqProvider.FieldHints.ShowBaseUrl);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(new object[] { "" })]
    [InlineData(new object[] { " " })]
    public async Task FetchLiveModelsAsync_EmptyApiKey_ReturnsNull(string? apiKey)
    {
        GroqProvider provider = new GroqProvider();
        Assert.Null(await provider.FetchLiveModelsAsync(apiKey, null, CancellationToken.None));
    }

    [Fact]
    public async Task FetchLiveModelsAsync_MapsAllModelsAsFreeToolCapableEntries()
    {
        using var server = new SimpleJsonServer((method, path) =>
            method == "GET" && path == "/v1/models"
                ? SimpleJsonServer.Json(200, """{"data":[{"id":"llama-3.3-70b"},{"id":"mixtral-8x7b"}]}""")
                : SimpleJsonServer.Json(404, """{"error":"missing"}"""));

        GroqProvider provider = new GroqProvider();

        var models = await provider.FetchLiveModelsAsync("groq-key", server.BaseUrl, CancellationToken.None);

        Assert.NotNull(models);
        Assert.Equal(new[] { "llama-3.3-70b", "mixtral-8x7b" }, models!.Select(m => m.Id).ToArray());
        Assert.All(models, m => Assert.Contains("tools", m.Capabilities));
        Assert.All(models, m => Assert.Contains("free", m.DisplayName, StringComparison.OrdinalIgnoreCase));
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
