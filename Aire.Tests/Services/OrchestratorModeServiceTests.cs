using System;
using System.Collections.Generic;
using Aire.Services;
using Xunit;

namespace Aire.Tests.Services;

public sealed class OrchestratorModeServiceTests
{
    [Fact]
    public void ShouldAutoApprove_UsesCanonicalToolCategories()
    {
        var orchestrator = new OrchestratorModeService();
        orchestrator.Start(allowedCategories: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "filesystem" });

        Assert.True(orchestrator.ShouldAutoApprove("list_directory"));
        Assert.True(orchestrator.ShouldAutoApprove("write_to_file"));
        Assert.False(orchestrator.ShouldAutoApprove("get_system_info"));
        Assert.False(orchestrator.ShouldAutoApprove("switch_model"));
    }

    [Fact]
    public void ShouldAutoApprove_RespectsAllowedCategorySet()
    {
        var orchestrator = new OrchestratorModeService();
        orchestrator.Start(allowedCategories: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "keyboard" });

        Assert.True(orchestrator.ShouldAutoApprove("type_text"));
        Assert.True(orchestrator.ShouldAutoApprove("key_press"));
        Assert.False(orchestrator.ShouldAutoApprove("take_screenshot"));
        Assert.False(orchestrator.ShouldAutoApprove("open_url"));
    }

    [Fact]
    public void ShouldAutoApprove_DeniesUnknownToolsWhenFilterIsApplied()
    {
        var orchestrator = new OrchestratorModeService();
        orchestrator.Start(allowedCategories: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "filesystem" });

        Assert.False(orchestrator.ShouldAutoApprove("search_docs"));
    }

    [Fact]
    public void RecordTaskFailure_StopsAfterThreeDifferentFailures()
    {
        var orchestrator = new OrchestratorModeService();
        string? blockedMessage = null;
        var blockedCount = 0;
        orchestrator.Blocked += message =>
        {
            blockedMessage = message;
            blockedCount++;
        };
        orchestrator.Start(goals: ["Ship orchestrator mode"]);

        orchestrator.RecordTaskFailure("provider-response", "timeout");
        orchestrator.RecordTaskFailure("provider-response", "rate limit");
        orchestrator.RecordTaskFailure("provider-response", "parse error");

        Assert.False(orchestrator.IsActive);
        Assert.Equal("blocked after repeated failures for 'provider-response'", orchestrator.StopReason);
        Assert.Equal(1, blockedCount);
        Assert.Contains("three different failed attempts", blockedMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }
}
