using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Aire.Data;
using Xunit;

namespace Aire.Tests.Core;

[Collection("NonParallelCoreUtilities")]
public sealed class ModelCatalogSyncTests : IDisposable
{
    private readonly string _modelsDir = Path.Combine(Path.GetTempPath(), $"aire_models_sync_{Guid.NewGuid():N}");

    public ModelCatalogSyncTests()
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
    public void SyncLiveModels_CreatesCatalogAndAddsAllModels_OnFirstImport()
    {
        var liveModels = new List<ModelDefinition>
        {
            new() { Id = "gpt-1", DisplayName = "GPT 1" },
            new() { Id = "gpt-2", DisplayName = "GPT 2" },
        };

        ModelCatalogSyncResult result = ModelCatalog.SyncLiveModels("OpenAI", liveModels);

        Assert.True(result.CreatedCatalog);
        Assert.Equal(new[] { "gpt-1", "gpt-2" }, result.AddedModelIds);
        Assert.Equal(2, ModelCatalog.GetDefaults("OpenAI").Count);
        Assert.True(Directory.GetFiles(_modelsDir, "models-live-OpenAI.json").Any());
    }

    [Fact]
    public void SyncLiveModels_AppendsNewModels_WithoutDroppingExistingEntries()
    {
        ModelCatalog.SyncLiveModels("OpenAI", new List<ModelDefinition>
        {
            new() { Id = "gpt-1", DisplayName = "GPT 1" },
            new() { Id = "gpt-2", DisplayName = "GPT 2" },
        });

        ModelCatalogSyncResult result = ModelCatalog.SyncLiveModels("OpenAI", new List<ModelDefinition>
        {
            new() { Id = "gpt-1", DisplayName = "GPT 1 renamed" },
            new() { Id = "gpt-2", DisplayName = "GPT 2" },
            new() { Id = "gpt-3", DisplayName = "GPT 3" },
        });

        Assert.False(result.CreatedCatalog);
        Assert.Equal(new[] { "gpt-3" }, result.AddedModelIds);

        var defaults = ModelCatalog.GetDefaults("OpenAI");
        Assert.Equal(3, defaults.Count);
        Assert.Contains(defaults, model => model.Id == "gpt-1" && model.DisplayName == "GPT 1 renamed");
    }
}
