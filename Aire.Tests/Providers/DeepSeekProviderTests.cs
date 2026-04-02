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

public class DeepSeekProviderTests
{
    private static string? TestApiKey => Environment.GetEnvironmentVariable("AIRE_TEST_DEEPSEEK_API_KEY");

    private DeepSeekProvider CreateInitialized(string apiKey, string baseUrl = "https://api.deepseek.com")
    {
        DeepSeekProvider deepSeekProvider = new DeepSeekProvider();
        deepSeekProvider.Initialize(new ProviderConfig
        {
            ApiKey = apiKey,
            BaseUrl = baseUrl,
            Model = "deepseek-chat"
        });
        return deepSeekProvider;
    }

    [Fact]
    public void ProviderType_IsDeepSeek()
    {
        DeepSeekProvider deepSeekProvider = new DeepSeekProvider();
        Assert.Equal("DeepSeek", deepSeekProvider.ProviderType);
    }

    [Fact]
    public void ProviderMetadata_IsExpected()
    {
        DeepSeekProvider deepSeekProvider = new DeepSeekProvider();

        Assert.Equal("DeepSeek (OpenAI‑compatible)", deepSeekProvider.DisplayName);
        Assert.False(deepSeekProvider.FieldHints.ShowBaseUrl);
        Assert.True(deepSeekProvider.Capabilities.HasFlag(ProviderCapabilities.ToolCalling));
        Assert.False(deepSeekProvider.SupportsImages);
    }

    [Fact]
    public async Task GetTokenUsageAsync_ReturnsValidTokenUsage()
    {
        if (string.IsNullOrWhiteSpace(TestApiKey))
        {
            return;
        }
        DeepSeekProvider provider = CreateInitialized(TestApiKey);
        TokenUsage tokenUsage = await provider.GetTokenUsageAsync(CancellationToken.None);
        if (tokenUsage != null)
        {
            Assert.True(tokenUsage.Used >= 0, $"Used ({tokenUsage.Used}) should be non-negative");
            Assert.True(tokenUsage.Limit > 0, $"Limit ({tokenUsage.Limit}) should be positive");
            Assert.Equal("USD", tokenUsage.Unit);
            Assert.InRange(tokenUsage.Percentage, 0.0, 1.0);
            if (tokenUsage.Limit.HasValue)
            {
                Assert.Equal(tokenUsage.Used >= tokenUsage.Limit.Value, tokenUsage.IsLimitReached);
            }
            Console.WriteLine($"DeepSeek Token Usage: Used={tokenUsage.Used} cents, Limit={tokenUsage.Limit} cents, Unit={tokenUsage.Unit}, Percentage={tokenUsage.Percentage:P}, IsLimitReached={tokenUsage.IsLimitReached}");
        }
    }

    [Fact]
    public async Task GetTokenUsageAsync_WithoutApiKey_ReturnsNull()
    {
        DeepSeekProvider provider = new DeepSeekProvider();
        Assert.Null(await provider.GetTokenUsageAsync(CancellationToken.None));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task FetchLiveModelsAsync_EmptyApiKey_ReturnsNull(string? apiKey)
    {
        DeepSeekProvider provider = new DeepSeekProvider();
        Assert.Null(await provider.FetchLiveModelsAsync(apiKey, null, CancellationToken.None));
    }

    [Fact]
    public async Task GetTokenUsageAsync_WithInvalidBaseUrlAndDummyKey_ReturnsNull()
    {
        DeepSeekProvider provider = CreateInitialized("dummy-key", "https://invalid.deepseek.com");

        Assert.Null(await provider.GetTokenUsageAsync(CancellationToken.None));
    }

    [Fact]
    public async Task FetchLiveModelsAsync_FiltersAndSortsDeepSeekModels()
    {
        using var server = new SimpleJsonServer((method, path) =>
            method == "GET" && path == "/v1/models"
                ? SimpleJsonServer.Json(200, """{"data":[{"id":"deepseek-reasoner"},{"id":"other-model"},{"id":"deepseek-chat"}]}""")
                : SimpleJsonServer.Json(404, """{"error":"missing"}"""));

        DeepSeekProvider provider = new DeepSeekProvider();

        var models = await provider.FetchLiveModelsAsync("deepseek-key", server.BaseUrl, CancellationToken.None);

        Assert.NotNull(models);
        Assert.Equal(new[] { "deepseek-reasoner", "deepseek-chat" }, models!.Select(m => m.Id).ToArray());
    }

    [Fact]
    public async Task FetchLiveModelsAsync_NonSuccessResponse_ReturnsNull()
    {
        using var server = new SimpleJsonServer((method, path) =>
            method == "GET" && path == "/v1/models"
                ? SimpleJsonServer.Json(503, """{"error":"busy"}""")
                : SimpleJsonServer.Json(404, """{"error":"missing"}"""));

        DeepSeekProvider provider = new DeepSeekProvider();

        Assert.Null(await provider.FetchLiveModelsAsync("deepseek-key", server.BaseUrl, CancellationToken.None));
    }

    [Fact]
    public async Task GetTokenUsageAsync_ParsesBalanceInfo()
    {
        using var server = new SimpleJsonServer((method, path) =>
            method == "GET" && path == "/user/balance"
                ? SimpleJsonServer.Json(200, """{"balance_infos":[{"total_balance":"2.25","granted_balance":"1.00","topped_up_balance":"3.00","currency":"USD"}]}""")
                : SimpleJsonServer.Json(404, """{"error":"missing"}"""));

        DeepSeekProvider provider = CreateInitialized("deepseek-key", server.BaseUrl);

        TokenUsage? usage = await provider.GetTokenUsageAsync(CancellationToken.None);

        Assert.NotNull(usage);
        Assert.Equal(175L, usage!.Used);
        Assert.Equal(400L, usage.Limit);
        Assert.Equal("USD", usage.Unit);
        Assert.False(usage.IsLimitReached);
    }

    [Fact]
    public async Task GetTokenUsageAsync_InvalidNumbers_ReturnsNull()
    {
        using var server = new SimpleJsonServer((method, path) =>
            method == "GET" && path == "/user/balance"
                ? SimpleJsonServer.Json(200, """{"balance_infos":[{"total_balance":"oops","granted_balance":"1.00","topped_up_balance":"3.00","currency":"USD"}]}""")
                : SimpleJsonServer.Json(404, """{"error":"missing"}"""));

        DeepSeekProvider provider = CreateInitialized("deepseek-key", server.BaseUrl);

        Assert.Null(await provider.GetTokenUsageAsync(CancellationToken.None));
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
