using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aire.Providers;
using Aire.Tests.Infrastructure;
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

}
