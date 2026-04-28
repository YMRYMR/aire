using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Aire.Data;
using Xunit;

namespace Aire.Tests.Core;

[Collection("NonParallelCoreUtilities")]
public class ModelCatalogTests : IDisposable
{
    private readonly string _modelsDir = Path.Combine(Path.GetTempPath(), $"aire_models_{Guid.NewGuid():N}");

    public ModelCatalogTests()
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
    public void GetModelsFolder_UsesEnvironmentOverride()
    {
        Assert.Equal(_modelsDir, ModelCatalog.GetModelsFolder());
    }

    [Fact]
    public void ImportFile_ValidCatalog_ReturnsModelCount()
    {
        Directory.CreateDirectory(_modelsDir);
        string text = Path.Combine(_modelsDir, "import.json");
        File.WriteAllText(text, "{\r\n  \"providerType\": \"OpenAI\",\r\n  \"models\": [\r\n    { \"id\": \"gpt-test-1\", \"displayName\": \"GPT Test 1\" },\r\n    { \"id\": \"gpt-test-2\", \"displayName\": \"GPT Test 2\" }\r\n  ]\r\n}");
        int actual = ModelCatalog.ImportFile(text);
        Assert.Equal(2, actual);
        Assert.True(Directory.EnumerateFiles(_modelsDir, "models-custom-*.json").Any());
    }

    [Fact]
    public void ImportFile_InvalidCatalog_ReturnsMinusOne()
    {
        Directory.CreateDirectory(_modelsDir);
        string text = Path.Combine(_modelsDir, "bad.json");
        File.WriteAllText(text, "{\"providerType\":\"\",\"models\":[]}");
        int actual = ModelCatalog.ImportFile(text);
        Assert.Equal(-1, actual);
    }

    [Fact]
    public void GetDefaults_ReturnsDistinctModelsForProvider()
    {
        Directory.CreateDirectory(_modelsDir);
        File.WriteAllText(Path.Combine(_modelsDir, "a.json"), "{\r\n  \"providerType\": \"OpenAI\",\r\n  \"models\": [\r\n    { \"id\": \"shared\", \"displayName\": \"Shared A\" },\r\n    { \"id\": \"unique-a\", \"displayName\": \"Unique A\" }\r\n  ]\r\n}");
        File.WriteAllText(Path.Combine(_modelsDir, "b.json"), "{\r\n  \"providerType\": \"openai\",\r\n  \"models\": [\r\n    { \"id\": \"shared\", \"displayName\": \"Shared B\" },\r\n    { \"id\": \"unique-b\", \"displayName\": \"Unique B\" }\r\n  ]\r\n}");
        File.WriteAllText(Path.Combine(_modelsDir, "c.json"), "{\r\n  \"providerType\": \"Anthropic\",\r\n  \"models\": [\r\n    { \"id\": \"claude\", \"displayName\": \"Claude\" }\r\n  ]\r\n}");
        List<ModelDefinition> defaults = ModelCatalog.GetDefaults("OpenAI");
        Assert.Equal(3, defaults.Count);
        Assert.Contains((IEnumerable<ModelDefinition>)defaults, (Predicate<ModelDefinition>)((ModelDefinition x) => x.Id == "shared"));
        Assert.Contains((IEnumerable<ModelDefinition>)defaults, (Predicate<ModelDefinition>)((ModelDefinition x) => x.Id == "unique-a"));
        Assert.Contains((IEnumerable<ModelDefinition>)defaults, (Predicate<ModelDefinition>)((ModelDefinition x) => x.Id == "unique-b"));
        Assert.DoesNotContain((IEnumerable<ModelDefinition>)defaults, (Predicate<ModelDefinition>)((ModelDefinition x) => x.Id == "claude"));
    }

    [Fact]
    public void GetDefaults_MergesLaterCapabilitiesAndContextLength_ForDuplicateModelIds()
    {
        Directory.CreateDirectory(_modelsDir);
        File.WriteAllText(Path.Combine(_modelsDir, "live.json"), "{\r\n  \"providerType\": \"DeepSeek\",\r\n  \"models\": [\r\n    { \"id\": \"deepseek-v4-pro\", \"displayName\": \"deepseek-v4-pro\" }\r\n  ]\r\n}");
        File.WriteAllText(Path.Combine(_modelsDir, "built-in.json"), "{\r\n  \"providerType\": \"DeepSeek\",\r\n  \"models\": [\r\n    { \"id\": \"deepseek-v4-pro\", \"displayName\": \"DeepSeek 4 Pro\", \"capabilities\": [\"tools\"], \"contextLength\": 128000 }\r\n  ]\r\n}");

        List<ModelDefinition> defaults = ModelCatalog.GetDefaults("DeepSeek");

        var model = Assert.Single(defaults);
        Assert.False(string.IsNullOrWhiteSpace(model.DisplayName));
        Assert.Contains("tools", model.Capabilities ?? []);
    }

    [Fact]
    public void EnsureDefaults_CreatesModelsDirectory()
    {
        ModelCatalog.EnsureDefaults();
        Assert.True(Directory.Exists(_modelsDir));
    }

    [Fact]
    public void GetContextLength_ReturnsNullForUnknownModel()
    {
        var result = ModelCatalog.GetContextLength("OpenAI", "unknown-model");
        Assert.Null(result);
    }

    [Fact]
    public void GetContextLength_ReturnsNullForEmptyProvider()
    {
        var result = ModelCatalog.GetContextLength("", "gpt-4o");
        Assert.Null(result);
    }

    [Fact]
    public void GetContextLength_ReturnsNullForEmptyModelId()
    {
        var result = ModelCatalog.GetContextLength("OpenAI", "");
        Assert.Null(result);
    }

    [Fact]
    public void GetContextLength_ReturnsValueWhenDefined()
    {
        Directory.CreateDirectory(_modelsDir);
        File.WriteAllText(Path.Combine(_modelsDir, "openai.json"), "{\r\n  \"providerType\": \"OpenAI\",\r\n  \"models\": [\r\n    { \"id\": \"gpt-4o\", \"displayName\": \"GPT-4o\", \"contextLength\": 128000 }\r\n  ]\r\n}");
        var result = ModelCatalog.GetContextLength("OpenAI", "gpt-4o");
        Assert.Equal(128000, result);
    }
}
