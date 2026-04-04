using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Aire.AppLayer.Abstractions;
using Aire.Data;
using Aire.Providers;
using Aire.Services;
using Xunit;

namespace Aire.Tests.Services;

[Collection("NonParallelCoreUtilities")]
public sealed class ProviderModelRefreshServiceTests : IDisposable
{
    private readonly string _modelsDir = Path.Combine(Path.GetTempPath(), $"aire_refresh_{Guid.NewGuid():N}");

    public ProviderModelRefreshServiceTests()
    {
        Environment.SetEnvironmentVariable("AIRE_MODELS_FOLDER", _modelsDir);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("AIRE_MODELS_FOLDER", null);
        try
        {
            if (Directory.Exists(_modelsDir))
            {
                Directory.Delete(_modelsDir, recursive: true);
            }
        }
        catch
        {
        }
    }

    [Fact]
    public async Task RefreshNowAsync_DoesNotNotify_WhenOnlyCreatingInitialCatalog()
    {
        var notifications = new List<(string Title, string Body)>();
        var service = CreateService(
            new[]
            {
                new Provider { Type = "OpenAI", Name = "OpenAI", Model = "gpt-1", IsEnabled = false }
            },
            new FakeProviderMetadata(new[]
            {
                new ModelDefinition { Id = "gpt-1", DisplayName = "GPT 1" }
            }),
            notifications);

        await service.RefreshNowAsync();

        Assert.Empty(notifications);
        Assert.Single(ModelCatalog.GetDefaults("OpenAI"));
    }

    [Fact]
    public async Task RefreshNowAsync_Notifies_WhenNewModelsAppear()
    {
        ModelCatalog.SyncLiveModels("OpenAI", new List<ModelDefinition>
        {
            new() { Id = "gpt-1", DisplayName = "GPT 1" }
        });

        var notifications = new List<(string Title, string Body)>();
        var service = CreateService(
            new[]
            {
                new Provider { Type = "OpenAI", Name = "OpenAI", Model = "gpt-1", IsEnabled = false }
            },
            new FakeProviderMetadata(new[]
            {
                new ModelDefinition { Id = "gpt-1", DisplayName = "GPT 1" },
                new ModelDefinition { Id = "gpt-2", DisplayName = "GPT 2" }
            }),
            notifications);

        await service.RefreshNowAsync();

        Assert.Single(notifications);
        Assert.Contains("OpenAI", notifications[0].Title, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("GPT 2", notifications[0].Body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, ModelCatalog.GetDefaults("OpenAI").Count);
    }

    private static ProviderModelRefreshService CreateService(
        IReadOnlyList<Provider> providers,
        IProviderMetadata metadata,
        List<(string Title, string Body)> notifications)
    {
        return new ProviderModelRefreshService(
            providerLoader: () => Task.FromResult(providers),
            metadataResolver: _ => metadata,
            notificationSink: (title, body) => notifications.Add((title, body)),
            refreshInterval: TimeSpan.FromMinutes(1));
    }

    private sealed class FakeProviderMetadata : IProviderMetadata
    {
        private readonly List<ModelDefinition> _models;

        public FakeProviderMetadata(IEnumerable<ModelDefinition> models)
        {
            _models = models.ToList();
        }

        public string ProviderType => "OpenAI";
        public string DisplayName => "OpenAI";
        public ProviderFieldHints FieldHints => new();
        public IReadOnlyList<ProviderAction> Actions => Array.Empty<ProviderAction>();
        public List<ModelDefinition> GetDefaultModels() => [];
        public Task<List<ModelDefinition>?> FetchLiveModelsAsync(string? apiKey, string? baseUrl, System.Threading.CancellationToken ct)
            => Task.FromResult<List<ModelDefinition>?>(_models.Select(model => new ModelDefinition
            {
                Id = model.Id,
                DisplayName = model.DisplayName,
                SizeBytes = model.SizeBytes,
                IsInstalled = model.IsInstalled,
                Capabilities = model.Capabilities?.ToList()
            }).ToList());
    }
}
