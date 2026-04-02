using System;
using System.Linq;
using Aire.AppLayer.Chat;
using Xunit;

namespace Aire.Tests.Services;

public class AssistantModeApplicationServiceTests
{
    [Fact]
    public void GetDefaultMode_ReturnsGeneral()
    {
        var service = new AssistantModeApplicationService();

        var mode = service.GetDefaultMode();

        Assert.Equal("general", mode.Key);
        Assert.Equal("General", mode.DisplayName);
    }

    [Fact]
    public void ResolveMode_FallsBackToGeneral_AndSupportsNewModes()
    {
        var service = new AssistantModeApplicationService();

        Assert.Equal("general", service.ResolveMode("missing").Key);
        Assert.Equal("security", service.ResolveMode("security").Key);
        Assert.Equal("scientist", service.ResolveMode("scientist").Key);
        Assert.Equal("psicologist", service.ResolveMode("psicologist").Key);
        Assert.Equal("philosopher", service.ResolveMode("philosopher").Key);
    }

    [Fact]
    public void BuildPromptSection_UsesResolvedModeInstruction()
    {
        var service = new AssistantModeApplicationService();

        var prompt = service.BuildPromptSection("architect");

        Assert.Contains("CURRENT OPERATING MODE: Architect", prompt, StringComparison.Ordinal);
        Assert.Contains("structure", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetModes_IncludesExpectedDisplayNames()
    {
        var service = new AssistantModeApplicationService();

        var displayNames = service.GetModes().Select(mode => mode.DisplayName).ToArray();

        Assert.Contains("Security", displayNames);
        Assert.Contains("Scientist", displayNames);
        Assert.Contains("Psicologist", displayNames);
        Assert.Contains("Philosopher", displayNames);
    }
}
