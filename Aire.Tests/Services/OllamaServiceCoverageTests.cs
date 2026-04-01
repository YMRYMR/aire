using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Aire.Services;
using Xunit;

namespace Aire.Tests.Services;

public class OllamaServiceCoverageTests : IDisposable
{
    private readonly OllamaService _service = new OllamaService();

    public void Dispose()
    {
        _service.Dispose();
    }

    [Fact]
    public void DefaultBaseUrl_IsLocalOllamaEndpoint()
    {
        Assert.Equal("http://localhost:11434", OllamaService.DefaultBaseUrl);
    }

    [Fact]
    public void KnownModelMeta_ContainsWellKnownModelsAndCapabilities()
    {
        Assert.True(OllamaService.KnownModelMeta.ContainsKey("qwen3:4b"));
        Assert.True(OllamaService.KnownModelMeta.ContainsKey("llama3.1:8b"));
        Assert.True(OllamaService.KnownModelMeta.ContainsKey("granite4:latest"));
        OllamaService.OllamaModelMeta ollamaModelMeta = OllamaService.KnownModelMeta["qwen3:4b"];
        OllamaService.OllamaModelMeta ollamaModelMeta2 = OllamaService.KnownModelMeta["llama3.1:8b"];
        Assert.True(ollamaModelMeta.Recommended);
        Assert.Contains("tools", (IEnumerable<string>)ollamaModelMeta.Tags);
        Assert.Contains("thinking", (IEnumerable<string>)ollamaModelMeta.Tags);
        Assert.Equal("4B", ollamaModelMeta.ParamSize);
        Assert.True(ollamaModelMeta.SizeBytes >= 0);
        Assert.True(ollamaModelMeta2.Recommended);
        Assert.Contains("tools", (IEnumerable<string>)ollamaModelMeta2.Tags);
        Assert.Equal("8B", ollamaModelMeta2.ParamSize);
    }

    [Fact]
    public void OllamaModelMeta_Record_StoresProvidedValues()
    {
        OllamaService.OllamaModelMeta ollamaModelMeta = new OllamaService.OllamaModelMeta(new string[] { "tools", "code" }, Recommended: true, "7B", 1234L);
        Assert.Equal<string[]>(new string[] { "tools", "code" }, ollamaModelMeta.Tags);
        Assert.True(ollamaModelMeta.Recommended);
        Assert.Equal("7B", ollamaModelMeta.ParamSize);
        Assert.Equal(1234L, ollamaModelMeta.SizeBytes);
    }

    [Fact]
    public void NestedDtoTypes_AreSerializableAndUsable()
    {
        OllamaService.OllamaModel ollamaModel = new OllamaService.OllamaModel
        {
            Name = "qwen3:4b",
            Size = 123L,
            Digest = "abc"
        };
        Assert.Equal("qwen3:4b", ollamaModel.Name);
        Assert.Equal(123L, ollamaModel.Size);
        Assert.Equal("abc", ollamaModel.Digest);
        OllamaService.OllamaPullProgress ollamaPullProgress = new OllamaService.OllamaPullProgress
        {
            Status = "downloading",
            Digest = "xyz",
            Total = 10L,
            Completed = 4L
        };
        Assert.Equal("downloading", ollamaPullProgress.Status);
        Assert.Equal("xyz", ollamaPullProgress.Digest);
        Assert.Equal(10L, ollamaPullProgress.Total);
        Assert.Equal(4L, ollamaPullProgress.Completed);
    }

    [Theory]
    [InlineData(new object[] { "http://127.0.0.1:1" })]
    [InlineData(new object[] { "http://localhost:1" })]
    public async Task IsOllamaReachableAsync_ReturnsFalseForUnavailableLocalEndpoints(string baseUrl)
    {
        Assert.False(await _service.IsOllamaReachableAsync(baseUrl, CancellationToken.None));
    }

    [Fact]
    public async Task IsOllamaReachableAsync_ReturnsFalseForInvalidUrl()
    {
        Assert.False(await _service.IsOllamaReachableAsync("http://[invalid", CancellationToken.None));
    }

    [Fact]
    public void StaticHelperMetadata_IsCompleteForCommonModelShapes()
    {
        IReadOnlyDictionary<string, OllamaService.OllamaModelMeta> knownModelMeta = OllamaService.KnownModelMeta;
        Assert.Contains<string>("qwen2.5-coder:7b", knownModelMeta.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.Contains<string>("mistral-nemo", knownModelMeta.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.Contains<string>("llava:7b", knownModelMeta.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.Contains<string>("nomic-embed-text", knownModelMeta.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("vision", (IEnumerable<string>)knownModelMeta["llava:7b"].Tags);
        Assert.Contains("embedding", (IEnumerable<string>)knownModelMeta["nomic-embed-text"].Tags);
        Assert.Contains("code", (IEnumerable<string>)knownModelMeta["qwen2.5-coder:7b"].Tags);
    }

    [Fact]
    public void GetModelRecommendation_PrefersBalancedToolModelsForMidrangeSystems()
    {
        OllamaService.OllamaSystemProfile profile = new OllamaService.OllamaSystemProfile(16.0, 120.0, 8.0, "Test GPU", "balanced", "test");
        OllamaService.OllamaModelRecommendation modelRecommendation = OllamaService.GetModelRecommendation("qwen3:4b", 0L, profile);
        Assert.True(modelRecommendation.RecommendedForThisPc);
        Assert.True(modelRecommendation.AireFriendly);
        Assert.False(modelRecommendation.TooLargeForThisPc);
        Assert.Contains<string>("best fit", modelRecommendation.Badges, StringComparer.OrdinalIgnoreCase);
        Assert.Contains<string>("tools", modelRecommendation.Badges, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetModelRecommendation_FlagsModelsThatAreTooLargeForLowRamSystems()
    {
        OllamaService.OllamaSystemProfile profile = new OllamaService.OllamaSystemProfile(8.0, 200.0, 4.0, "Test GPU", "starter", "test");
        OllamaService.OllamaModelRecommendation modelRecommendation = OllamaService.GetModelRecommendation("qwen3:32b", 0L, profile);
        Assert.False(modelRecommendation.RecommendedForThisPc);
        Assert.True(modelRecommendation.TooLargeForThisPc);
        Assert.Equal("too large", modelRecommendation.SummaryLabel);
    }

    [Fact]
    public void GetModelRecommendation_DemotesEmbeddingOnlyModelsForAire()
    {
        OllamaService.OllamaSystemProfile profile = new OllamaService.OllamaSystemProfile(32.0, 200.0, 12.0, "Test GPU", "strong", "test");
        OllamaService.OllamaModelRecommendation modelRecommendation = OllamaService.GetModelRecommendation("nomic-embed-text", 0L, profile);
        Assert.False(modelRecommendation.RecommendedForThisPc);
        Assert.False(modelRecommendation.AireFriendly);
        Assert.Equal("specialized", modelRecommendation.SummaryLabel);
    }

    [Fact]
    public void GetModelRecommendation_FlagsDiskPressure()
    {
        OllamaService.OllamaSystemProfile profile = new OllamaService.OllamaSystemProfile(32.0, 5.0, 12.0, "Test GPU", "strong", "test");
        OllamaService.OllamaModelRecommendation modelRecommendation = OllamaService.GetModelRecommendation("gemma3:4b", 0L, profile);
        Assert.False(modelRecommendation.RecommendedForThisPc);
        Assert.True(modelRecommendation.DiskSpaceLikelyInsufficient);
        Assert.Equal("needs more disk", modelRecommendation.SummaryLabel);
    }

    [Fact]
    public void FormatHardwareSummary_IncludesGpuNameWhenAvailable()
    {
        OllamaService.OllamaSystemProfile profile = new OllamaService.OllamaSystemProfile(32.0, 251.3, 8.0, "NVIDIA GeForce RTX 3050", "strong", "test");
        string actualString = OllamaService.FormatHardwareSummary(profile);
        Assert.Contains("32 GB RAM", actualString);
        Assert.Contains("8 GB VRAM (NVIDIA GeForce RTX 3050)", actualString);
        Assert.Contains("GB free disk", actualString);
    }
}
