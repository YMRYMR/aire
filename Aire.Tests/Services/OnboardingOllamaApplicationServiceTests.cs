using System;
using System.Collections.Generic;
using System.Linq;
using Aire.AppLayer.Providers;
using Aire.Services;
using Xunit;

namespace Aire.Tests.Services;

public sealed class OnboardingOllamaApplicationServiceTests
{
    private readonly OnboardingOllamaApplicationService _sut = new();

    // ── BuildHardwareGuidance ─────────────────────────────────────────────

    [Fact]
    public void BuildHardwareGuidance_ZeroRam_ProducesFallbackText()
    {
        var profile = MakeProfile(totalRamGb: 0);

        var guidance = _sut.BuildHardwareGuidance(profile);

        Assert.Equal("Could not detect RAM. Most models need at least 4 GB.", guidance.SummaryLine);
        Assert.Null(guidance.WarningLine);
    }

    [Fact]
    public void BuildHardwareGuidance_NegativeRam_ProducesFallbackText()
    {
        var profile = MakeProfile(totalRamGb: -1);

        var guidance = _sut.BuildHardwareGuidance(profile);

        Assert.Equal("Could not detect RAM. Most models need at least 4 GB.", guidance.SummaryLine);
        Assert.Null(guidance.WarningLine);
    }

    [Fact]
    public void BuildHardwareGuidance_LowRam_ProducesWarning()
    {
        var profile = MakeProfile(totalRamGb: 2);

        var guidance = _sut.BuildHardwareGuidance(profile);

        Assert.Contains("2", guidance.SummaryLine);
        Assert.Contains("smaller local models", guidance.SummaryLine);
        Assert.NotNull(guidance.WarningLine);
        Assert.Contains("GB RAM", guidance.WarningLine!);
        Assert.Contains("at least 4 GB", guidance.WarningLine!);
    }

    [Fact]
    public void BuildHardwareGuidance_EightGbRam_SaysSmallAndMedium()
    {
        var profile = MakeProfile(totalRamGb: 8);

        var guidance = _sut.BuildHardwareGuidance(profile);

        Assert.Contains("8", guidance.SummaryLine);
        Assert.Contains("small and medium local models", guidance.SummaryLine);
        Assert.Null(guidance.WarningLine);
    }

    [Fact]
    public void BuildHardwareGuidance_SixteenGbRam_SaysMediumAndSomeLarger()
    {
        var profile = MakeProfile(totalRamGb: 16);

        var guidance = _sut.BuildHardwareGuidance(profile);

        Assert.Contains("16", guidance.SummaryLine);
        Assert.Contains("medium and some larger local models", guidance.SummaryLine);
        Assert.Null(guidance.WarningLine);
    }

    [Fact]
    public void BuildHardwareGuidance_ThirtyTwoGbRam_SaysMediumAndLarger()
    {
        var profile = MakeProfile(totalRamGb: 32);

        var guidance = _sut.BuildHardwareGuidance(profile);

        Assert.Contains("32", guidance.SummaryLine);
        Assert.Contains("medium and larger local models", guidance.SummaryLine);
        Assert.Null(guidance.WarningLine);
    }

    [Fact]
    public void BuildHardwareGuidance_SixtyFourGbRam_SaysMediumAndLarger()
    {
        var profile = MakeProfile(totalRamGb: 64);

        var guidance = _sut.BuildHardwareGuidance(profile);

        Assert.Contains("64", guidance.SummaryLine);
        Assert.Contains("medium and larger local models", guidance.SummaryLine);
    }

    [Fact]
    public void BuildHardwareGuidance_WithVram_IncludesGpuInfo()
    {
        var profile = MakeProfile(totalRamGb: 16, videoRamGb: 8, gpuName: "RTX 4070");

        var guidance = _sut.BuildHardwareGuidance(profile);

        Assert.Contains("RTX 4070", guidance.SummaryLine);
        Assert.Contains("8", guidance.SummaryLine);
        Assert.Contains("VRAM", guidance.SummaryLine);
    }

    [Fact]
    public void BuildHardwareGuidance_WithVramButNoGpuName_IncludesVramOnly()
    {
        var profile = MakeProfile(totalRamGb: 16, videoRamGb: 6, gpuName: "");

        var guidance = _sut.BuildHardwareGuidance(profile);

        Assert.Contains("6", guidance.SummaryLine);
        Assert.Contains("VRAM", guidance.SummaryLine);
        Assert.DoesNotContain("on ", guidance.SummaryLine);
    }

    [Fact]
    public void BuildHardwareGuidance_WithoutVram_OmitsVram()
    {
        var profile = MakeProfile(totalRamGb: 16, videoRamGb: 0);

        var guidance = _sut.BuildHardwareGuidance(profile);

        Assert.DoesNotContain("VRAM", guidance.SummaryLine);
        Assert.Contains("RAM", guidance.SummaryLine);
    }

    [Fact]
    public void BuildHardwareGuidance_FourGbRam_NoWarning()
    {
        // Exactly 4 GB should NOT trigger the low-RAM warning (< 4)
        var profile = MakeProfile(totalRamGb: 4);

        var guidance = _sut.BuildHardwareGuidance(profile);

        Assert.Null(guidance.WarningLine);
    }

    // ── BuildCatalog ─────────────────────────────────────────────────────

    [Fact]
    public void BuildCatalog_EmptyLists_ProducesReadyTextWithoutInstalledCount()
    {
        var profile = MakeProfile(totalRamGb: 16);
        var installed = new List<OllamaService.OllamaModel>();
        var available = new List<OllamaService.OllamaModel>();

        var catalog = _sut.BuildCatalog(installed, available, profile);

        Assert.Contains("Ollama is running on your machine", catalog.ReadyText);
        Assert.DoesNotContain("models installed", catalog.ReadyText);
        Assert.DoesNotContain("model installed", catalog.ReadyText);
        Assert.Contains("No models installed yet", catalog.HintText);
        Assert.Empty(catalog.Entries);
    }

    [Fact]
    public void BuildCatalog_WithInstalledModels_ProducesReadyTextWithCount()
    {
        var profile = MakeProfile(totalRamGb: 16);
        var installed = new List<OllamaService.OllamaModel>
        {
            new() { Name = "llama3.1", Size = 0 },
        };
        var available = new List<OllamaService.OllamaModel>();

        var catalog = _sut.BuildCatalog(installed, available, profile);

        Assert.Contains("1 model installed", catalog.ReadyText);
        Assert.Contains("installed", catalog.HintText);
    }

    [Fact]
    public void BuildCatalog_MultipleInstalledModels_UsesPlural()
    {
        var profile = MakeProfile(totalRamGb: 16);
        var installed = new List<OllamaService.OllamaModel>
        {
            new() { Name = "llama3.1", Size = 0 },
            new() { Name = "mistral", Size = 0 },
        };
        var available = new List<OllamaService.OllamaModel>();

        var catalog = _sut.BuildCatalog(installed, available, profile);

        Assert.Contains("2 models installed", catalog.ReadyText);
    }

    [Fact]
    public void BuildCatalog_InstalledModelsListedFirst()
    {
        var profile = MakeProfile(totalRamGb: 16);
        var installed = new List<OllamaService.OllamaModel>
        {
            new() { Name = "mistral", Size = 0 },
        };
        var available = new List<OllamaService.OllamaModel>
        {
            new() { Name = "gemma3", Size = 0 },
            new() { Name = "phi4", Size = 0 },
        };

        var catalog = _sut.BuildCatalog(installed, available, profile);

        Assert.NotEmpty(catalog.Entries);
        Assert.True(catalog.Entries[0].IsInstalled);
        Assert.Equal("mistral", catalog.Entries[0].ModelName);
    }

    [Fact]
    public void BuildCatalog_Ordering_InstalledFirstThenRecommendedThenAlphabetical()
    {
        var profile = MakeProfile(totalRamGb: 16);
        var installed = new List<OllamaService.OllamaModel>
        {
            new() { Name = "zeta-model", Size = 0 },
        };
        var available = new List<OllamaService.OllamaModel>
        {
            new() { Name = "alpha-model", Size = 0 },
            new() { Name = "phi4", Size = 0 },
        };

        var catalog = _sut.BuildCatalog(installed, available, profile);

        // Installed first
        Assert.Equal("zeta-model", catalog.Entries[0].ModelName);
        Assert.True(catalog.Entries[0].IsInstalled);

        // The rest should be ordered: recommended before non-recommended,
        // then alphabetically.
        var nonInstalled = catalog.Entries.Where(e => !e.IsInstalled).ToList();
        var recommendedFirst = nonInstalled.TakeWhile(e => e.IsRecommended).ToList();
        var rest = nonInstalled.SkipWhile(e => e.IsRecommended).ToList();

        // Recommended entries are in alphabetical order.
        Assert.Equal(recommendedFirst.OrderBy(e => e.ModelName, StringComparer.OrdinalIgnoreCase),
                     recommendedFirst);
    }

    [Fact]
    public void BuildCatalog_EntryContainsCorrectFields()
    {
        var profile = MakeProfile(totalRamGb: 16);
        var installed = new List<OllamaService.OllamaModel>();
        var available = new List<OllamaService.OllamaModel>
        {
            new() { Name = "phi4", Size = 9_100_274_688L },
        };

        var catalog = _sut.BuildCatalog(installed, available, profile);

        Assert.Single(catalog.Entries);
        var entry = catalog.Entries[0];
        Assert.Equal("phi4", entry.ModelName);
        Assert.False(entry.IsInstalled);
        Assert.NotEmpty(entry.SizeText);
        // phi4 has KnownModelMeta with ParamSize "14B"
        Assert.Equal("14B", entry.ParameterSize);
        // phi4 has the "tools" tag so Tags should not be empty
        Assert.NotEmpty(entry.Tags);
    }

    [Fact]
    public void BuildCatalog_DeduplicatesInstalledFromAvailable()
    {
        var profile = MakeProfile(totalRamGb: 16);
        var installed = new List<OllamaService.OllamaModel>
        {
            new() { Name = "llama3.1", Size = 0 },
        };
        var available = new List<OllamaService.OllamaModel>
        {
            new() { Name = "llama3.1", Size = 0 },
            new() { Name = "phi4", Size = 0 },
        };

        var catalog = _sut.BuildCatalog(installed, available, profile);

        // llama3.1 should appear once (as installed), phi4 once (as available)
        var names = catalog.Entries.Select(e => e.ModelName).ToList();
        Assert.Single(names, n => n == "llama3.1");
        Assert.Single(names, n => n == "phi4");
        Assert.Equal(2, names.Count);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static OllamaService.OllamaSystemProfile MakeProfile(
        double totalRamGb = 16,
        double freeDiskGb = 100,
        double videoRamGb = 0,
        string gpuName = "",
        string performanceTier = "Medium",
        string summary = "")
    {
        return new OllamaService.OllamaSystemProfile(
            totalRamGb, freeDiskGb, videoRamGb, gpuName, performanceTier, summary);
    }
}
