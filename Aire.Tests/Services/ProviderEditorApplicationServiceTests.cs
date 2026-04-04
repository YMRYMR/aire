using Aire.AppLayer.Providers;
using Xunit;

namespace Aire.Tests.Services;

public sealed class ProviderEditorApplicationServiceTests
{
    [Fact]
    public void BuildTypeChangePlan_LoadsMetadataModels_ForNonOllamaEditableProviders()
    {
        var service = new ProviderEditorApplicationService();

        var plan = service.BuildTypeChangePlan("OpenAI", hasSelectedProvider: true, isRefreshing: false);

        Assert.Equal("OpenAI", plan.Metadata.ProviderType);
        Assert.Equal(ProviderEditorApplicationService.ModelLoadAction.LoadMetadataModels, plan.ModelAction);
    }

    [Fact]
    public void BuildSelectionPlan_PreservesEditorState_AndChoosesExpectedLoadAction()
    {
        var service = new ProviderEditorApplicationService();
        var provider = new Aire.Data.Provider
        {
            Name = "OpenAI Main",
            Type = "OpenAI",
            ApiKey = "sk-test",
            BaseUrl = "https://example.test",
            Model = "gpt-5.4-mini",
            IsEnabled = true
        };

        var plan = service.BuildSelectionPlan(provider, isRefreshing: false);
        var refreshingPlan = service.BuildSelectionPlan(provider, isRefreshing: true);

        Assert.Equal("OpenAI Main", plan.Name);
        Assert.Equal("OpenAI", plan.Type);
        Assert.Equal("sk-test", plan.ApiKey);
        Assert.Equal("https://example.test", plan.BaseUrl);
        Assert.Equal("gpt-5.4-mini", plan.Model);
        Assert.True(plan.IsEnabled);
        Assert.True(plan.HasApiKey);
        Assert.Equal(ProviderEditorApplicationService.ModelLoadAction.LoadMetadataModels, plan.ModelAction);
        Assert.Equal(ProviderEditorApplicationService.ModelLoadAction.None, refreshingPlan.ModelAction);
    }

    [Fact]
    public void BuildTypeChangePlan_SuppressesModelReload_ForOllamaOrRefresh()
    {
        var service = new ProviderEditorApplicationService();

        var ollamaPlan = service.BuildTypeChangePlan("Ollama", hasSelectedProvider: true, isRefreshing: false);
        var refreshPlan = service.BuildTypeChangePlan("OpenAI", hasSelectedProvider: true, isRefreshing: true);
        var noSelectionPlan = service.BuildTypeChangePlan("OpenAI", hasSelectedProvider: false, isRefreshing: false);

        Assert.Equal(ProviderEditorApplicationService.ModelLoadAction.None, ollamaPlan.ModelAction);
        Assert.Equal(ProviderEditorApplicationService.ModelLoadAction.None, refreshPlan.ModelAction);
        Assert.Equal(ProviderEditorApplicationService.ModelLoadAction.None, noSelectionPlan.ModelAction);
    }

    [Fact]
    public void BuildOllamaSelectionPlan_MatchesKnownModel_AndDisablesDownloadWhenMissing()
    {
        var service = new ProviderEditorApplicationService();

        var matched = service.BuildOllamaSelectionPlan(new[] { "qwen3:4b", "llama3.2:3b" }, "Qwen3:4B");
        var missing = service.BuildOllamaSelectionPlan(new[] { "qwen3:4b" }, "new-model");

        Assert.Equal("qwen3:4b", matched.SelectedModelName);
        Assert.False(matched.EnableDownloadButton);
        Assert.Null(missing.SelectedModelName);
        Assert.True(missing.EnableDownloadButton);
    }
}
