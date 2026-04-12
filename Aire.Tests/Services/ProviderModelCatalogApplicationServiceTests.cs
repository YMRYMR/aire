using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Aire.AppLayer.Providers;
using Aire.Data;
using Aire.Providers;
using Xunit;

namespace Aire.Tests.Services;

public sealed class ProviderModelCatalogApplicationServiceTests
{
    private static readonly ModelDefinition[] DefaultModels =
    [
        new() { Id = "default-1", DisplayName = "Default 1" },
        new() { Id = "default-2", DisplayName = "Default 2" }
    ];

    private static readonly ModelDefinition[] LiveModels =
    [
        new() { Id = "live-a", DisplayName = "Live A" },
        new() { Id = "live-b", DisplayName = "Live B" },
        new() { Id = "live-c", DisplayName = "Live C" }
    ];

    private readonly ProviderModelCatalogApplicationService _sut = new();

    // --- Logic path 1: No API key returns defaults immediately ---

    [Fact]
    public async Task LoadModelsAsync_NullApiKey_ReturnsDefaultsWithoutLiveLookup()
    {
        var metadata = new FakeMetadata(DefaultModels, liveModels: LiveModels);

        var result = await _sut.LoadModelsAsync(metadata, apiKey: null, baseUrl: null, CancellationToken.None);

        Assert.False(result.UsedLiveModels);
        Assert.Null(result.StatusMessage);
        Assert.Equal(DefaultModels, result.DefaultModels);
        Assert.Equal(DefaultModels, result.EffectiveModels);
        Assert.False(metadata.FetchWasCalled);
    }

    [Fact]
    public async Task LoadModelsAsync_EmptyApiKey_ReturnsDefaultsWithoutLiveLookup()
    {
        var metadata = new FakeMetadata(DefaultModels, liveModels: LiveModels);

        var result = await _sut.LoadModelsAsync(metadata, apiKey: "", baseUrl: null, CancellationToken.None);

        Assert.False(result.UsedLiveModels);
        Assert.Null(result.StatusMessage);
        Assert.Equal(DefaultModels, result.EffectiveModels);
        Assert.False(metadata.FetchWasCalled);
    }

    [Fact]
    public async Task LoadModelsAsync_WhitespaceApiKey_ReturnsDefaultsWithoutLiveLookup()
    {
        var metadata = new FakeMetadata(DefaultModels, liveModels: LiveModels);

        var result = await _sut.LoadModelsAsync(metadata, apiKey: "   ", baseUrl: null, CancellationToken.None);

        Assert.False(result.UsedLiveModels);
        Assert.Null(result.StatusMessage);
        Assert.Equal(DefaultModels, result.EffectiveModels);
        Assert.False(metadata.FetchWasCalled);
    }

    // --- Logic path 2: Live models available ---

    [Fact]
    public async Task LoadModelsAsync_LiveModelsAvailable_ReturnsLiveAsEffective()
    {
        var metadata = new FakeMetadata(DefaultModels, liveModels: LiveModels);

        var result = await _sut.LoadModelsAsync(metadata, apiKey: "sk-test", baseUrl: null, CancellationToken.None);

        Assert.True(result.UsedLiveModels);
        Assert.Equal(LiveModels, result.EffectiveModels);
        Assert.Equal(DefaultModels, result.DefaultModels);
    }

    [Fact]
    public async Task LoadModelsAsync_LiveModelsAvailable_StatusContainsCountAndProviderType()
    {
        var metadata = new FakeMetadata(DefaultModels, liveModels: LiveModels);

        var result = await _sut.LoadModelsAsync(metadata, apiKey: "sk-test", baseUrl: "https://example.com", CancellationToken.None);

        Assert.Contains("3", result.StatusMessage);
        Assert.Contains(metadata.ProviderType, result.StatusMessage);
    }

    [Fact]
    public async Task LoadModelsAsync_PassesApiKeyAndBaseUrl_ToFetchLiveModels()
    {
        var metadata = new FakeMetadata(DefaultModels, liveModels: LiveModels);

        await _sut.LoadModelsAsync(metadata, apiKey: "sk-test", baseUrl: "https://custom.api", CancellationToken.None);

        Assert.Equal("sk-test", metadata.LastApiKey);
        Assert.Equal("https://custom.api", metadata.LastBaseUrl);
    }

    // --- Logic path 3: Live models empty/null falls back to defaults ---

    [Fact]
    public async Task LoadModelsAsync_LiveModelsEmpty_FallsBackToDefaults()
    {
        var metadata = new FakeMetadata(DefaultModels, liveModels: []);

        var result = await _sut.LoadModelsAsync(metadata, apiKey: "sk-test", baseUrl: null, CancellationToken.None);

        Assert.False(result.UsedLiveModels);
        Assert.Equal(DefaultModels, result.EffectiveModels);
        Assert.NotNull(result.StatusMessage);
    }

    [Fact]
    public async Task LoadModelsAsync_LiveModelsNull_FallsBackToDefaults()
    {
        var metadata = new FakeMetadata(DefaultModels, liveModels: null);

        var result = await _sut.LoadModelsAsync(metadata, apiKey: "sk-test", baseUrl: null, CancellationToken.None);

        Assert.False(result.UsedLiveModels);
        Assert.Equal(DefaultModels, result.EffectiveModels);
        Assert.NotNull(result.StatusMessage);
    }

    [Fact]
    public async Task LoadModelsAsync_LiveModelsEmpty_StatusIndicatesFallback()
    {
        var metadata = new FakeMetadata(DefaultModels, liveModels: []);

        var result = await _sut.LoadModelsAsync(metadata, apiKey: "sk-test", baseUrl: null, CancellationToken.None);

        Assert.Contains("built-in", result.StatusMessage);
    }

    // --- Logic path 4: OperationCanceledException is re-thrown ---

    [Fact]
    public async Task LoadModelsAsync_OperationCanceledException_Rethrown()
    {
        var metadata = new FakeMetadata(DefaultModels, liveModels: LiveModels);
        metadata.ThrowOnFetch = new OperationCanceledException();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _sut.LoadModelsAsync(metadata, apiKey: "sk-test", baseUrl: null, CancellationToken.None));
    }

    [Fact]
    public async Task LoadModelsAsync_CancellationTriggers_RethrowsOperationCanceled()
    {
        var cts = new CancellationTokenSource();
        var metadata = new FakeMetadata(DefaultModels, liveModels: LiveModels);
        metadata.ThrowOnFetch = new OperationCanceledException(cts.Token);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _sut.LoadModelsAsync(metadata, apiKey: "sk-test", baseUrl: null, cts.Token));
    }

    // --- Logic path 5: Generic exception caught, returns default fallback ---

    [Fact]
    public async Task LoadModelsAsync_GenericException_ReturnsDefaultFallback()
    {
        var metadata = new FakeMetadata(DefaultModels, liveModels: LiveModels);
        metadata.ThrowOnFetch = new HttpRequestException("network error");

        var result = await _sut.LoadModelsAsync(metadata, apiKey: "sk-test", baseUrl: null, CancellationToken.None);

        Assert.False(result.UsedLiveModels);
        Assert.Equal(DefaultModels, result.EffectiveModels);
    }

    [Fact]
    public async Task LoadModelsAsync_GenericException_StatusIndicatesFallback()
    {
        var metadata = new FakeMetadata(DefaultModels, liveModels: LiveModels);
        metadata.ThrowOnFetch = new InvalidOperationException("something broke");

        var result = await _sut.LoadModelsAsync(metadata, apiKey: "sk-test", baseUrl: null, CancellationToken.None);

        Assert.Contains("built-in", result.StatusMessage);
    }

    [Fact]
    public async Task LoadModelsAsync_TimeoutException_ReturnsDefaultFallback()
    {
        var metadata = new FakeMetadata(DefaultModels, liveModels: LiveModels);
        metadata.ThrowOnFetch = new TimeoutException("timed out");

        var result = await _sut.LoadModelsAsync(metadata, apiKey: "sk-test", baseUrl: null, CancellationToken.None);

        Assert.False(result.UsedLiveModels);
        Assert.Equal(DefaultModels, result.EffectiveModels);
        Assert.NotNull(result.StatusMessage);
    }

    // --- Inline fake ---

    private sealed class FakeMetadata : IProviderMetadata
    {
        private readonly List<ModelDefinition> _defaultModels;
        private readonly List<ModelDefinition>? _liveModels;

        public FakeMetadata(ModelDefinition[] defaultModels, ModelDefinition[]? liveModels)
        {
            _defaultModels = [.. defaultModels];
            _liveModels = liveModels is null ? null : [.. liveModels];
        }

        public string ProviderType => "TestProvider";
        public string DisplayName => "Test Provider";
        public ProviderFieldHints FieldHints => new();
        public IReadOnlyList<ProviderAction> Actions => Array.Empty<ProviderAction>();

        public bool FetchWasCalled { get; private set; }
        public string? LastApiKey { get; private set; }
        public string? LastBaseUrl { get; private set; }
        public Exception? ThrowOnFetch { get; set; }

        public List<ModelDefinition> GetDefaultModels() => _defaultModels;

        public Task<List<ModelDefinition>?> FetchLiveModelsAsync(string? apiKey, string? baseUrl, CancellationToken ct)
        {
            FetchWasCalled = true;
            LastApiKey = apiKey;
            LastBaseUrl = baseUrl;

            if (ThrowOnFetch is not null)
                throw ThrowOnFetch;

            return Task.FromResult(_liveModels);
        }
    }
}
