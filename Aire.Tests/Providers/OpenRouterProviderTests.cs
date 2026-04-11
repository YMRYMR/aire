using System;
using System.Threading;
using System.Threading.Tasks;
using Aire.Providers;
using Aire.Tests.Infrastructure;
using Xunit;

namespace Aire.Tests.Providers;

public class OpenRouterProviderTests
{
    [Fact]
    public async Task GetTokenUsageAsync_ParsesKeyUsagePayload()
    {
        using var server = new SimpleJsonServer((method, path) =>
            method == "GET" && path == "/v1/key"
                ? SimpleJsonServer.Json(200, """{"data":{"usage":25.5,"limit_remaining":74.5,"reset_date":"2026-04-30T00:00:00Z"}}""")
                : SimpleJsonServer.Json(404, """{"error":"missing"}"""));

        var provider = new OpenRouterProvider();
        provider.Initialize(new ProviderConfig
        {
            ApiKey = "or-test",
            BaseUrl = server.BaseUrl,
            Model = "openai/gpt-4o-mini"
        });

        var usage = await provider.GetTokenUsageAsync(CancellationToken.None);

        Assert.NotNull(usage);
        Assert.Equal(2550L, usage!.Used);
        Assert.Equal(10000L, usage.Limit);
        Assert.Equal("credits", usage.Unit);
        Assert.Equal(new DateTime(2026, 4, 30, 0, 0, 0, DateTimeKind.Utc), usage.ResetDate?.ToUniversalTime());
    }

    [Fact]
    public async Task GetTokenUsageAsync_FallsBackToDefaultBaseUrlWhenBaseUrlIsBlank()
    {
        using var server = new SimpleJsonServer((method, path) =>
            method == "GET" && path == "/v1/key"
                ? SimpleJsonServer.Json(200, """{"data":{"usage":1.25,"limit_remaining":8.75}}""")
                : SimpleJsonServer.Json(404, """{"error":"missing"}"""));

        var provider = new TestOpenRouterProvider(server.BaseUrl);
        provider.Initialize(new ProviderConfig
        {
            ApiKey = "or-test",
            BaseUrl = "",
            Model = "openai/gpt-4o-mini"
        });

        var usage = await provider.GetTokenUsageAsync(CancellationToken.None);

        Assert.NotNull(usage);
        Assert.Equal(125L, usage!.Used);
        Assert.Equal(1000L, usage.Limit);
        Assert.Equal("credits", usage.Unit);
    }

    private sealed class TestOpenRouterProvider : OpenRouterProvider
    {
        private readonly string _defaultBaseUrl;

        public TestOpenRouterProvider(string defaultBaseUrl)
        {
            _defaultBaseUrl = defaultBaseUrl;
        }

        protected override string DefaultApiBaseUrl => _defaultBaseUrl;
    }
}
