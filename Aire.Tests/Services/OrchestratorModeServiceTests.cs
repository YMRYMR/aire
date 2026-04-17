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

    [Fact]
    public void RecordTokenUsage_AutoStopsWhenBudgetExceeded()
    {
        var orchestrator = new OrchestratorModeService();
        var budgetExhausted = false;
        orchestrator.BudgetExhausted += () => budgetExhausted = true;

        orchestrator.Start(tokenBudget: 100);
        orchestrator.RecordTokenUsage(150);

        Assert.False(orchestrator.IsActive);
        Assert.Contains("budget", orchestrator.StopReason ?? "", StringComparison.OrdinalIgnoreCase);
        Assert.True(budgetExhausted);
    }

    [Fact]
    public void MarkGoalCompleted_StopsWhenAllGoalsDone()
    {
        var orchestrator = new OrchestratorModeService();
        orchestrator.Start(goals: ["Goal A", "Goal B"]);

        orchestrator.MarkGoalCompleted("Goal A");
        Assert.True(orchestrator.IsActive);

        orchestrator.MarkGoalCompleted("Goal B");
        Assert.False(orchestrator.IsActive);
        Assert.Equal("goals completed", orchestrator.StopReason);
    }

    [Fact]
    public void MarkGoalCompleted_DoesNothingWhenNotActive()
    {
        var orchestrator = new OrchestratorModeService();
        var goalCompletedCount = 0;
        orchestrator.GoalCompleted += _ => goalCompletedCount++;

        // Calling on an inactive service should not throw or fire events
        orchestrator.MarkGoalCompleted("nonexistent");
        Assert.Equal(0, goalCompletedCount);
    }

    [Fact]
    public void SetGoals_ReplacesExistingGoals()
    {
        var orchestrator = new OrchestratorModeService();
        orchestrator.Start(goals: ["A", "B"]);

        orchestrator.SetGoals(["C"]);

        Assert.Single(orchestrator.Goals);
        Assert.Equal("C", orchestrator.Goals[0]);
    }

    [Fact]
    public void BuildSnapshot_ReflectsCurrentState()
    {
        var orchestrator = new OrchestratorModeService();
        orchestrator.Start(tokenBudget: 5000, goals: ["Write tests"], allowedCategories: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "filesystem" });

        var snapshot = orchestrator.BuildSnapshot(conversationId: 42, lastNarrative: "Working on it");

        Assert.Equal(42, snapshot.ConversationId);
        Assert.Equal(5000, snapshot.TokenBudget);
        Assert.Single(snapshot.Goals);
        Assert.Contains("filesystem", snapshot.SelectedCategories);
        Assert.Equal("Working on it", snapshot.LastNarrative);
    }

    [Fact]
    public void Start_WithRestoreSnapshot_RebuildsState()
    {
        var orchestrator1 = new OrchestratorModeService();
        orchestrator1.Start(tokenBudget: 3000, goals: ["Original goal"]);
        orchestrator1.RecordTokenUsage(500);

        var snapshot = orchestrator1.BuildSnapshot();
        orchestrator1.Stop();

        var orchestrator2 = new OrchestratorModeService();
        orchestrator2.Start(restoreSnapshot: snapshot);

        Assert.Equal(3000, orchestrator2.TokenBudget);
        Assert.Equal(500, orchestrator2.TokensConsumed);
        Assert.Single(orchestrator2.Goals);
        Assert.Equal("Original goal", orchestrator2.Goals[0]);
        orchestrator2.Stop();
    }

    [Fact]
    public void ClearTaskFailures_ResolvesBlockingState()
    {
        var orchestrator = new OrchestratorModeService();
        orchestrator.Start(goals: ["Test goal"]);

        orchestrator.RecordTaskFailure("task-1", "err-a");
        orchestrator.RecordTaskFailure("task-1", "err-b");
        orchestrator.RecordTaskFailure("task-1", "err-c");
        Assert.False(orchestrator.IsActive);

        // Clear failures and restart
        orchestrator.ClearTaskFailures("task-1");
        Assert.Empty(orchestrator.GetTaskFailureSignatures("task-1"));
    }

    [Fact]
    public void RecordProviderFailure_MarksUnhealthyAfterThreshold()
    {
        var orchestrator = new OrchestratorModeService();
        Assert.True(orchestrator.IsProviderHealthy("provider-1"));

        orchestrator.RecordProviderFailure("provider-1");
        orchestrator.RecordProviderFailure("provider-1");
        Assert.True(orchestrator.IsProviderHealthy("provider-1"));

        orchestrator.RecordProviderFailure("provider-1");
        Assert.False(orchestrator.IsProviderHealthy("provider-1"));
    }

    [Fact]
    public void RecordProviderSuccess_ResetsFailureCount()
    {
        var orchestrator = new OrchestratorModeService();

        orchestrator.RecordProviderFailure("provider-1");
        orchestrator.RecordProviderFailure("provider-1");
        orchestrator.RecordProviderSuccess("provider-1");
        orchestrator.RecordProviderFailure("provider-1");
        orchestrator.RecordProviderFailure("provider-1");

        // Only 2 consecutive failures after reset — still healthy
        Assert.True(orchestrator.IsProviderHealthy("provider-1"));
    }

    [Fact]
    public void ReportProgress_ReturnsCurrentState()
    {
        var orchestrator = new OrchestratorModeService();
        orchestrator.Start(tokenBudget: 1000, goals: ["Goal 1", "Goal 2"]);

        var report = orchestrator.ReportProgress("Goal 1");

        Assert.Equal(2, report.GoalsTotal);
        Assert.Equal(0, report.TokensConsumed);
        Assert.Equal(1000, report.TokenBudget);
        Assert.Equal("Goal 1", report.CurrentGoal);
        Assert.Null(report.StopReason);
        orchestrator.Stop();
    }
}
