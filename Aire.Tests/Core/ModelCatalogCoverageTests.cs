using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Aire.Data;
using Xunit;

namespace Aire.Tests.Core;

public class ModelCatalogCoverageTests : IDisposable
{
    private readonly string _modelsFolder;

    public ModelCatalogCoverageTests()
    {
        _modelsFolder = Path.Combine(Path.GetTempPath(), $"aire-models-{Guid.NewGuid():N}");
        Environment.SetEnvironmentVariable("AIRE_MODELS_FOLDER", _modelsFolder);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("AIRE_MODELS_FOLDER", null);
        try
        {
            if (Directory.Exists(_modelsFolder))
            {
                Directory.Delete(_modelsFolder, recursive: true);
            }
        }
        catch
        {
        }
    }

    [Fact]
    public void GetModelsFolder_UsesEnvironmentOverride()
    {
        Assert.Equal(_modelsFolder, ModelCatalog.GetModelsFolder());
    }

    [Fact]
    public void EnsureDefaults_ExtractsEmbeddedCatalogFiles()
    {
        ModelCatalog.EnsureDefaults();
        Assert.True(Directory.Exists(_modelsFolder));
        Assert.NotEmpty(Directory.GetFiles(_modelsFolder, "*.json"));
    }

    [Fact]
    public void GetDefaults_MergesFilesAndDeduplicatesIds()
    {
        Directory.CreateDirectory(_modelsFolder);
        File.WriteAllText(Path.Combine(_modelsFolder, "a.json"), "{\r\n  \"providerType\": \"OpenAI\",\r\n  \"models\": [\r\n    { \"id\": \"gpt-1\", \"displayName\": \"One\" },\r\n    { \"id\": \"gpt-2\", \"displayName\": \"Two\" }\r\n  ]\r\n}");
        File.WriteAllText(Path.Combine(_modelsFolder, "b.json"), "{\r\n  \"providerType\": \"OpenAI\",\r\n  \"models\": [\r\n    { \"id\": \"gpt-2\", \"displayName\": \"Duplicate\" },\r\n    { \"id\": \"gpt-3\", \"displayName\": \"Three\" }\r\n  ]\r\n}");
        File.WriteAllText(Path.Combine(_modelsFolder, "c.json"), "{\r\n  \"providerType\": \"Anthropic\",\r\n  \"models\": [\r\n    { \"id\": \"claude-1\", \"displayName\": \"Claude\" }\r\n  ]\r\n}");
        List<ModelDefinition> defaults = ModelCatalog.GetDefaults("OpenAI");
        Assert.Equal(3, defaults.Count);
        Assert.Equal<string[]>(new string[] { "gpt-1", "gpt-2", "gpt-3" }, defaults.Select((ModelDefinition model) => model.Id).ToArray());
    }

    [Fact]
    public void GetDefaults_SkipsMalformedFiles()
    {
        Directory.CreateDirectory(_modelsFolder);
        File.WriteAllText(Path.Combine(_modelsFolder, "broken.json"), "{not json");
        List<ModelDefinition> defaults = ModelCatalog.GetDefaults("OpenAI");
        Assert.Empty(defaults);
    }

    [Fact]
    public void ImportFile_ValidCatalog_CopiesFileAndReturnsModelCount()
    {
        Directory.CreateDirectory(_modelsFolder);
        string text = Path.Combine(_modelsFolder, "import-source.json");
        File.WriteAllText(text, "{\r\n  \"providerType\": \"OpenAI\",\r\n  \"models\": [\r\n    { \"id\": \"gpt-x\", \"displayName\": \"X\" },\r\n    { \"id\": \"gpt-y\", \"displayName\": \"Y\" }\r\n  ]\r\n}");
        int actual = ModelCatalog.ImportFile(text);
        Assert.Equal(2, actual);
        Assert.True(Directory.GetFiles(_modelsFolder, "models-custom-*.json").Length >= 1);
    }

    [Fact]
    public void ImportFile_InvalidCatalog_ReturnsMinusOne()
    {
        Directory.CreateDirectory(_modelsFolder);
        string text = Path.Combine(_modelsFolder, "invalid.json");
        File.WriteAllText(text, "{\"providerType\":\"\",\"models\":[]}");
        int actual = ModelCatalog.ImportFile(text);
        Assert.Equal(-1, actual);
    }
}
