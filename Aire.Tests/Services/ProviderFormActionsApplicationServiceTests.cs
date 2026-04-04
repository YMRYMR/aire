using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aire.AppLayer.Abstractions;
using Aire.AppLayer.Providers;
using Aire.Data;
using Aire.Domain.Providers;
using Aire.Providers;
using Xunit;

namespace Aire.Tests.Services;

public sealed class ProviderFormActionsApplicationServiceTests
{
    private sealed class FakeMetadata(List<ModelDefinition> defaultModels) : IProviderMetadata
    {
        public string ProviderType => "OpenAI";
        public string DisplayName => "OpenAI";
        public ProviderFieldHints FieldHints { get; } = new() { ShowApiKey = true, ApiKeyRequired = true, ShowBaseUrl = true };
        public IReadOnlyList<ProviderAction> Actions { get; } = [];
        public List<ModelDefinition>? LiveModels { get; init; }

        public List<ModelDefinition> GetDefaultModels() => defaultModels;

        public Task<List<ModelDefinition>?> FetchLiveModelsAsync(string? apiKey, string? baseUrl, CancellationToken ct)
            => Task.FromResult(LiveModels);
    }

    private sealed class FakeCodexManagementClient : ICodexManagementClient
    {
        public bool InstallCalled { get; private set; }

        public Task<CodexCliStatus> GetStatusAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new CodexCliStatus(false, null, false, "Codex CLI not found."));

        public Task InstallAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
        {
            InstallCalled = true;
            progress?.Report("Installing Codex CLI…");
            progress?.Report("Codex CLI installed.");
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task LoadModelsAsync_ReturnsSharedCatalogResult()
    {
        var service = new ProviderFormActionsApplicationService();
        var metadata = new FakeMetadata(
        [
            new ModelDefinition { Id = "model-a", DisplayName = "Model A" },
            new ModelDefinition { Id = "model-b", DisplayName = "Model B" }
        ]);

        var result = await service.LoadModelsAsync(metadata, "sk-test", "https://example.test");

        Assert.Equal(2, result.DefaultModels.Count);
        Assert.Equal(new[] { "model-a", "model-b" }, result.DefaultModels.Select(m => m.Id).ToArray());
        Assert.False(result.UsedLiveModels);
    }

    [Fact]
    public async Task LoadModelsAsync_UsesLiveModels_WhenMetadataReturnsThem()
    {
        var service = new ProviderFormActionsApplicationService();
        var metadata = new FakeMetadata(
        [
            new ModelDefinition { Id = "model-a", DisplayName = "Model A" }
        ])
        {
            LiveModels =
            [
                new ModelDefinition { Id = "live-a", DisplayName = "Live A" },
                new ModelDefinition { Id = "live-b", DisplayName = "Live B" }
            ]
        };

        var result = await service.LoadModelsAsync(metadata, "sk-test", "https://example.test");

        Assert.True(result.UsedLiveModels);
        Assert.Equal(new[] { "live-a", "live-b" }, result.EffectiveModels.Select(m => m.Id).ToArray());
        Assert.Contains("fetched", result.StatusMessage ?? string.Empty);
    }

    [Fact]
    public void GetProviderToolStatus_ReturnsCodexInstallStatus()
    {
        var service = new ProviderFormActionsApplicationService();

        var status = service.GetProviderToolStatus("Codex");

        Assert.NotNull(status);
        Assert.Equal("Install Codex CLI", status!.ActionLabel);
        Assert.False(string.IsNullOrWhiteSpace(status.StatusMessage));
    }

    [Fact]
    public void GetProviderToolStatus_ReturnsNullForProvidersWithoutManagedTool()
    {
        var service = new ProviderFormActionsApplicationService();

        Assert.Null(service.GetProviderToolStatus("OpenAI"));
    }

    [Fact]
    public async Task InstallProviderToolAsync_DelegatesToCodexActionWorkflow()
    {
        var client = new FakeCodexManagementClient();
        var service = new ProviderFormActionsApplicationService(new CodexActionApplicationService(client));
        var updates = new List<string>();

        var result = await service.InstallProviderToolAsync("Codex", new Progress<string>(updates.Add));

        Assert.True(result.Succeeded);
        Assert.True(client.InstallCalled);
        Assert.NotEmpty(updates);
    }

    [Fact]
    public async Task InstallProviderToolAsync_RejectsUnsupportedProviderTypes()
    {
        var service = new ProviderFormActionsApplicationService();

        var result = await service.InstallProviderToolAsync("OpenAI");

        Assert.False(result.Succeeded);
        Assert.Contains("No installable provider tool", result.UserMessage);
    }
}
