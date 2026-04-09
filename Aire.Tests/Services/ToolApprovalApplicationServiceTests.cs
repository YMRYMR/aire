using System;
using System.Text.Json;
using System.Threading.Tasks;
using Aire.AppLayer.Tools;
using Aire.Services;
using Aire.Services.Policies;
using Xunit;

namespace Aire.Tests.Services;

public sealed class ToolApprovalApplicationServiceTests
{
    [Fact]
    public async Task DetermineAutoApproveAsync_UsesActiveSessionsBeforePolicyFallback()
    {
        var policy = new ToolAutoAcceptPolicyService(() => Task.FromResult("{\"Enabled\":true,\"AllowedTools\":[\"read_file\"],\"AllowMouseTools\":false,\"AllowKeyboardTools\":false}"));
        var service = new ToolApprovalApplicationService(policy);
        var now = new DateTime(2026, 4, 9, 10, 0, 0, DateTimeKind.Local);

        var keyboardSession = new ToolApprovalSessionState(
            MouseSessionActive: false,
            MouseSessionExpiry: now,
            KeyboardSessionActive: true,
            KeyboardSessionExpiry: now.AddMinutes(10));

        var mouseSession = new ToolApprovalSessionState(
            MouseSessionActive: true,
            MouseSessionExpiry: now.AddMinutes(10),
            KeyboardSessionActive: false,
            KeyboardSessionExpiry: now);

        var keyboardDecision = await service.DetermineAutoApproveAsync("type_text", keyboardSession, now);
        var mouseDecision = await service.DetermineAutoApproveAsync("click", mouseSession, now);

        Assert.True(keyboardDecision.AutoApprove);
        Assert.True(mouseDecision.AutoApprove);
        Assert.Equal(keyboardSession, keyboardDecision.SessionState);
        Assert.Equal(mouseSession, mouseDecision.SessionState);
    }

    [Fact]
    public async Task DetermineAutoApproveAsync_ClearsExpiredSessions_AndFallsBackToPolicy()
    {
        var policy = new ToolAutoAcceptPolicyService(() => Task.FromResult("{\"Enabled\":true,\"AllowedTools\":[\"write_file\"],\"AllowMouseTools\":true,\"AllowKeyboardTools\":false}"));
        var service = new ToolApprovalApplicationService(policy);
        var now = new DateTime(2026, 4, 9, 10, 0, 0, DateTimeKind.Local);

        var expiredMouseSession = new ToolApprovalSessionState(
            MouseSessionActive: true,
            MouseSessionExpiry: now.AddMinutes(-1),
            KeyboardSessionActive: false,
            KeyboardSessionExpiry: now);

        var decision = await service.DetermineAutoApproveAsync("click", expiredMouseSession, now);

        Assert.True(decision.AutoApprove);
        Assert.False(decision.SessionState.MouseSessionActive);
        Assert.Equal("Mouse session expired.", decision.SessionStatusMessage);
    }

    [Fact]
    public async Task DetermineAutoApproveAsync_RespectsPolicyAliasesAndFamilies()
    {
        var policy = new ToolAutoAcceptPolicyService(() => Task.FromResult("{\"Enabled\":true,\"AllowedTools\":[\"write_file\"],\"AllowMouseTools\":true,\"AllowKeyboardTools\":false}"));
        var service = new ToolApprovalApplicationService(policy);
        var now = new DateTime(2026, 4, 9, 10, 0, 0, DateTimeKind.Local);
        var noSession = new ToolApprovalSessionState(false, now, false, now);

        Assert.True((await service.DetermineAutoApproveAsync("write_to_file", noSession, now)).AutoApprove);
        Assert.True((await service.DetermineAutoApproveAsync("click", noSession, now)).AutoApprove);
        Assert.False((await service.DetermineAutoApproveAsync("type_text", noSession, now)).AutoApprove);
    }

    [Fact]
    public async Task DetermineAutoApproveAsync_ReturnsFalse_WhenPolicyPayloadIsInvalid()
    {
        var policy = new ToolAutoAcceptPolicyService(() => Task.FromResult("{not-json"));
        var service = new ToolApprovalApplicationService(policy);
        var now = new DateTime(2026, 4, 9, 10, 0, 0, DateTimeKind.Local);

        var decision = await service.DetermineAutoApproveAsync("read_file", new ToolApprovalSessionState(false, now, false, now), now);

        Assert.False(decision.AutoApprove);
        Assert.Equal(new ToolApprovalSessionState(false, now, false, now), decision.SessionState);
    }

    [Fact]
    public void ApplySessionState_UsesExplicitAndDefaultDurations_AndCanEndSessions()
    {
        var policy = new ToolAutoAcceptPolicyService(() => Task.FromResult<string?>(null));
        var service = new ToolApprovalApplicationService(policy);
        var now = new DateTime(2026, 4, 9, 12, 0, 0, DateTimeKind.Local);
        var initial = new ToolApprovalSessionState(false, now, false, now);

        var keyboardStarted = service.ApplySessionState(BuildRequest("begin_keyboard_session", "{\"duration_minutes\":3}"), initial, now);
        var keyboardEnded = service.ApplySessionState(BuildRequest("end_keyboard_session", "{}"), keyboardStarted, now);
        var mouseStarted = service.ApplySessionState(BuildRequest("begin_mouse_session", "{\"duration_minutes\":-1}"), keyboardEnded, now);
        var mouseEnded = service.ApplySessionState(BuildRequest("end_mouse_session", "{}"), mouseStarted, now);

        Assert.True(keyboardStarted.KeyboardSessionActive);
        Assert.Equal(now.AddMinutes(3), keyboardStarted.KeyboardSessionExpiry);
        Assert.False(keyboardEnded.KeyboardSessionActive);
        Assert.True(mouseStarted.MouseSessionActive);
        Assert.Equal(now.AddMinutes(5), mouseStarted.MouseSessionExpiry);
        Assert.False(mouseEnded.MouseSessionActive);
    }

    private static ToolCallRequest BuildRequest(string tool, string json)
        => new()
        {
            Tool = tool,
            Parameters = JsonDocument.Parse(json).RootElement.Clone()
        };
}
