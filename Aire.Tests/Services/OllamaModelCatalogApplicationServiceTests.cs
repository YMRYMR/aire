using System;
using System.Collections.Generic;
using System.Linq;
using Aire.AppLayer.Providers;
using Aire.Services;
using Xunit;

namespace Aire.Tests.Services;

public sealed class OllamaModelCatalogApplicationServiceTests
{
    private readonly OllamaModelCatalogApplicationService _service = new();

    // -----------------------------------------------------------------
    // FormatModelSize
    // -----------------------------------------------------------------

    [Fact]
    public void FormatModelSize_Zero_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, OllamaModelCatalogApplicationService.FormatModelSize(0));
    }

    [Fact]
    public void FormatModelSize_Negative_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, OllamaModelCatalogApplicationService.FormatModelSize(-1));
        Assert.Equal(string.Empty, OllamaModelCatalogApplicationService.FormatModelSize(-999_999));
    }

    [Fact]
    public void FormatModelSize_ExactlyOneGB_FormatsWithOneDecimal()
    {
        long oneGb = 1L * 1024 * 1024 * 1024; // 1_073_741_824
        var result = OllamaModelCatalogApplicationService.FormatModelSize(oneGb);
        // Accept both period and comma decimal separators (locale-dependent)
        Assert.True(result is "1.0 GB" or "1,0 GB", $"Unexpected format: '{result}'");
    }

    [Fact]
    public void FormatModelSize_SubGB_FormatsAsWholeMB()
    {
        // 512 MB = 536_870_912 bytes
        long halfGb = 512L * 1024 * 1024;
        Assert.Equal("512 MB", OllamaModelCatalogApplicationService.FormatModelSize(halfGb));
    }

    [Fact]
    public void FormatModelSize_SmallBytes_FormatsAsMB()
    {
        // 1 MB = 1_048_576 bytes
        Assert.Equal("1 MB", OllamaModelCatalogApplicationService.FormatModelSize(1_048_576));
    }

    [Fact]
    public void FormatModelSize_JustBelow1GB_FormatsAsMB()
    {
        // 1 GB - 1 byte = 1_073_741_823 bytes → 1023.something MB, rounds to "1024 MB"
        long justBelow = 1_073_741_824L - 1;
        var result = OllamaModelCatalogApplicationService.FormatModelSize(justBelow);
        // The division 1_073_741_823 / 1_048_576 = 1023.999... which rounds to 1024
        Assert.True(result is "1023 MB" or "1024 MB", $"Unexpected format: '{result}'");
    }

    [Fact]
    public void FormatModelSize_LargeMultiGB_FormatsCorrectly()
    {
        // 4.7 GB
        long size = (long)(4.7 * 1024 * 1024 * 1024);
        var result = OllamaModelCatalogApplicationService.FormatModelSize(size);
        // Accept both period and comma decimal separators (locale-dependent)
        Assert.True(result.StartsWith("4.7") || result.StartsWith("4,7"),
            $"Unexpected format: '{result}'");
        Assert.EndsWith("GB", result);
    }

    [Fact]
    public void FormatModelSize_OneByte_FormatsAsMB()
    {
        // 1 byte → rounds to 0 MB
        Assert.Equal("0 MB", OllamaModelCatalogApplicationService.FormatModelSize(1));
    }

    // -----------------------------------------------------------------
    // BuildCatalog – installed models get checkmark prefix
    // -----------------------------------------------------------------

    [Fact]
    public void BuildCatalog_InstalledModel_HasCheckmarkPrefix()
    {
        var installed = new List<OllamaService.OllamaModel>
        {
            new() { Name = "llama3", Size = 4_000_000_000 }
        };
        var available = new List<OllamaService.OllamaModel>();
        var profile = MakeProfile();

        var result = _service.BuildCatalog(installed, available, profile);

        Assert.Single(result);
        Assert.StartsWith("\u2713 ", result[0].DisplayName); // ✓
        Assert.True(result[0].IsInstalled);
        Assert.Equal("llama3", result[0].ModelName);
    }

    // -----------------------------------------------------------------
    // BuildCatalog – deduplication
    // -----------------------------------------------------------------

    [Fact]
    public void BuildCatalog_DuplicateName_InstalledTakesPrecedence()
    {
        var installed = new List<OllamaService.OllamaModel>
        {
            new() { Name = "mistral", Size = 4_000_000_000 }
        };
        var available = new List<OllamaService.OllamaModel>
        {
            new() { Name = "mistral", Size = 4_000_000_000 }
        };
        var profile = MakeProfile();

        var result = _service.BuildCatalog(installed, available, profile);

        Assert.Single(result);
        Assert.True(result[0].IsInstalled);
        Assert.Equal("mistral", result[0].ModelName);
    }

    [Fact]
    public void BuildCatalog_DuplicateName_CaseInsensitive()
    {
        var installed = new List<OllamaService.OllamaModel>
        {
            new() { Name = "Gemma2", Size = 5_000_000_000 }
        };
        var available = new List<OllamaService.OllamaModel>
        {
            new() { Name = "gemma2", Size = 5_000_000_000 }
        };
        var profile = MakeProfile();

        var result = _service.BuildCatalog(installed, available, profile);

        Assert.Single(result);
        Assert.True(result[0].IsInstalled);
    }

    // -----------------------------------------------------------------
    // BuildCatalog – ordering (installed first, then available, sorted)
    // -----------------------------------------------------------------

    [Fact]
    public void BuildCatalog_InstalledBeforeAvailable_SortedWithinGroups()
    {
        var installed = new List<OllamaService.OllamaModel>
        {
            new() { Name = "zephyr", Size = 4_000_000_000 },
            new() { Name = "alpha-model", Size = 2_000_000_000 }
        };
        var available = new List<OllamaService.OllamaModel>
        {
            new() { Name = "bravo", Size = 3_000_000_000 },
            new() { Name = "charlie", Size = 1_000_000_000 }
        };
        var profile = MakeProfile();

        var result = _service.BuildCatalog(installed, available, profile);

        Assert.Equal(4, result.Count);

        // Installed group (sorted): alpha-model, zephyr
        Assert.Equal("alpha-model", result[0].ModelName);
        Assert.True(result[0].IsInstalled);
        Assert.Equal("zephyr", result[1].ModelName);
        Assert.True(result[1].IsInstalled);

        // Available group (sorted): bravo, charlie
        Assert.Equal("bravo", result[2].ModelName);
        Assert.False(result[2].IsInstalled);
        Assert.Equal("charlie", result[3].ModelName);
        Assert.False(result[3].IsInstalled);
    }

    // -----------------------------------------------------------------
    // BuildCatalog – empty inputs
    // -----------------------------------------------------------------

    [Fact]
    public void BuildCatalog_NoModels_ReturnsEmptyList()
    {
        var profile = MakeProfile();

        var result = _service.BuildCatalog(
            Array.Empty<OllamaService.OllamaModel>(),
            Array.Empty<OllamaService.OllamaModel>(),
            profile);

        Assert.Empty(result);
    }

    [Fact]
    public void BuildCatalog_OnlyAvailable_NoCheckmarks()
    {
        var available = new List<OllamaService.OllamaModel>
        {
            new() { Name = "phi3", Size = 2_000_000_000 }
        };
        var profile = MakeProfile();

        var result = _service.BuildCatalog(
            Array.Empty<OllamaService.OllamaModel>(),
            available,
            profile);

        Assert.Single(result);
        Assert.False(result[0].IsInstalled);
        // Should not start with checkmark
        Assert.DoesNotContain("\u2713", result[0].DisplayName);
    }

    // -----------------------------------------------------------------
    // BuildCatalog – size text propagation
    // -----------------------------------------------------------------

    [Fact]
    public void BuildCatalog_InstalledModel_ContainsSizeInDisplayName()
    {
        var installed = new List<OllamaService.OllamaModel>
        {
            new() { Name = "codellama", Size = 3_221_225_472 } // ~3.0 GB
        };
        var available = new List<OllamaService.OllamaModel>();
        var profile = MakeProfile();

        var result = _service.BuildCatalog(installed, available, profile);

        Assert.Single(result);
        // Accept both period and comma decimal separators (locale-dependent)
        Assert.True(
            result[0].DisplayName.Contains("3.0 GB") || result[0].DisplayName.Contains("3,0 GB"),
            $"Expected size in display name but got: '{result[0].DisplayName}'");
        Assert.True(
            result[0].SizeText is "3.0 GB" or "3,0 GB",
            $"Unexpected SizeText: '{result[0].SizeText}'");
    }

    [Fact]
    public void BuildCatalog_ZeroSizeModel_NoSizeInDisplayName()
    {
        var installed = new List<OllamaService.OllamaModel>
        {
            new() { Name = "tiny", Size = 0 }
        };
        var available = new List<OllamaService.OllamaModel>();
        var profile = MakeProfile();

        var result = _service.BuildCatalog(installed, available, profile);

        Assert.Single(result);
        Assert.Equal(string.Empty, result[0].SizeText);
        // DisplayName should start with checkmark prefix and model name (no parenthetical size).
        // A recommendation label suffix may follow.
        Assert.StartsWith("\u2713 tiny", result[0].DisplayName);
        Assert.DoesNotContain("(", result[0].DisplayName);
    }

    // -----------------------------------------------------------------
    // BuildCatalog – case-insensitive ordering
    // -----------------------------------------------------------------

    [Fact]
    public void BuildCatalog_SortingIsCaseInsensitive()
    {
        var installed = new List<OllamaService.OllamaModel>
        {
            new() { Name = "Beta", Size = 1_000_000_000 },
            new() { Name = "alpha", Size = 1_000_000_000 }
        };
        var available = new List<OllamaService.OllamaModel>();
        var profile = MakeProfile();

        var result = _service.BuildCatalog(installed, available, profile);

        Assert.Equal(2, result.Count);
        Assert.Equal("alpha", result[0].ModelName);
        Assert.Equal("Beta", result[1].ModelName);
    }

    // -----------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------

    private static OllamaService.OllamaSystemProfile MakeProfile(
        double totalRamGb = 32.0,
        double freeDiskGb = 500.0,
        double videoRamGb = 12.0,
        string gpuName = "Test GPU",
        string tier = "High",
        string summary = "Test system")
    {
        return new OllamaService.OllamaSystemProfile(
            totalRamGb, freeDiskGb, videoRamGb, gpuName, tier, summary);
    }
}
