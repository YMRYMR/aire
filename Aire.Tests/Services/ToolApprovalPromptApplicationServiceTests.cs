using Aire.AppLayer.Tools;
using Xunit;

namespace Aire.Tests.Services;

public sealed class ToolApprovalPromptApplicationServiceTests
{
    private readonly ToolApprovalPromptApplicationService _sut = new();

    // --- BuildPromptPlan ---

    [Fact]
    public void BuildPromptPlan_AutoApprove_True_WindowVisible_True_NoPending_NoReveal()
    {
        var plan = _sut.BuildPromptPlan(autoApprove: true, isWindowVisible: true);

        Assert.False(plan.IsApprovalPending);
        Assert.True(plan.AutoApproveImmediately);
        Assert.False(plan.ShouldRevealWindow);
    }

    [Fact]
    public void BuildPromptPlan_AutoApprove_True_WindowVisible_False_NoPending_NoReveal()
    {
        var plan = _sut.BuildPromptPlan(autoApprove: true, isWindowVisible: false);

        Assert.False(plan.IsApprovalPending);
        Assert.True(plan.AutoApproveImmediately);
        Assert.False(plan.ShouldRevealWindow);
    }

    [Fact]
    public void BuildPromptPlan_AutoApprove_False_WindowVisible_True_Pending_NoReveal()
    {
        var plan = _sut.BuildPromptPlan(autoApprove: false, isWindowVisible: true);

        Assert.True(plan.IsApprovalPending);
        Assert.False(plan.AutoApproveImmediately);
        Assert.False(plan.ShouldRevealWindow);
    }

    [Fact]
    public void BuildPromptPlan_AutoApprove_False_WindowVisible_False_Pending_Reveal()
    {
        var plan = _sut.BuildPromptPlan(autoApprove: false, isWindowVisible: false);

        Assert.True(plan.IsApprovalPending);
        Assert.False(plan.AutoApproveImmediately);
        Assert.True(plan.ShouldRevealWindow);
    }

    // --- BuildCompletionPlan ---

    [Fact]
    public void BuildCompletionPlan_Approved_Includes_Description_And_NotDenied()
    {
        var plan = _sut.BuildCompletionPlan(approved: true, toolDescription: "Read file foo.txt");

        Assert.Contains("Read file foo.txt", plan.ToolCallStatus);
        Assert.StartsWith("\u2713", plan.ToolCallStatus);
        Assert.False(plan.WasDenied);
    }

    [Fact]
    public void BuildCompletionPlan_Denied_ShowsDenied_And_WasDenied()
    {
        var plan = _sut.BuildCompletionPlan(approved: false, toolDescription: "Read file foo.txt");

        Assert.Equal("\u2717 Denied", plan.ToolCallStatus);
        Assert.True(plan.WasDenied);
    }
}
