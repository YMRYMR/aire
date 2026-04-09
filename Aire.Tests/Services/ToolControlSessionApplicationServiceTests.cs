using System;
using System.Text.Json;
using System.Threading.Tasks;
using Aire.AppLayer.Tools;
using Aire.Services;
using Aire.Services.Policies;
using Xunit;

namespace Aire.Tests.Services;

public sealed class ToolControlSessionApplicationServiceTests
{
    [Fact]
    public void BuildBannerPlan_ShowsActiveMouseSession_AndUsesCeilingMinutes()
    {
        var service = new ToolControlSessionApplicationService(CreateApprovalService("{\"Enabled\":false}"));
        var now = new DateTime(2026, 4, 9, 12, 0, 0, DateTimeKind.Local);

        service.ApplyToolRequest(BuildRequest("begin_mouse_session", "{\"duration_minutes\":2}"), now);
        var banner = service.BuildBannerPlan(now.AddSeconds(30));

        Assert.True(banner.IsVisible);
        Assert.True(banner.SessionActive);
        Assert.Equal("Mouse session active — expires in ~2 min", banner.BannerText);
    }

    [Fact]
    public void BuildBannerPlan_ShowsKeyboardSession_WhenKeyboardIsActive()
    {
        var service = new ToolControlSessionApplicationService(CreateApprovalService("{\"Enabled\":false}"));
        var now = new DateTime(2026, 4, 9, 12, 0, 0, DateTimeKind.Local);

        service.ApplyToolRequest(BuildRequest("begin_keyboard_session", "{\"duration_minutes\":1}"), now);
        var banner = service.BuildBannerPlan(now.AddSeconds(10));

        Assert.True(banner.IsVisible);
        Assert.True(banner.SessionActive);
        Assert.Equal("Keyboard session active — expires in ~1 min", banner.BannerText);
    }

    [Fact]
    public void BuildBannerPlan_ClearsExpiredSessions_AndReturnsHiddenBanner()
    {
        var service = new ToolControlSessionApplicationService(CreateApprovalService("{\"Enabled\":false}"));
        var now = new DateTime(2026, 4, 9, 12, 0, 0, DateTimeKind.Local);

        service.ApplyToolRequest(BuildRequest("begin_mouse_session", "{\"duration_minutes\":1}"), now);
        var banner = service.BuildBannerPlan(now.AddMinutes(2));

        Assert.False(banner.IsVisible);
        Assert.False(banner.SessionActive);
        Assert.Null(banner.BannerText);
    }

    [Fact]
    public async Task DetermineAutoApproveAsync_PersistsSessionStateAcrossChecks_AndStopClearsIt()
    {
        var service = new ToolControlSessionApplicationService(CreateApprovalService("{\"Enabled\":true,\"AllowedTools\":[\"read_file\"],\"AllowMouseTools\":false,\"AllowKeyboardTools\":false}"));
        var now = new DateTime(2026, 4, 9, 12, 0, 0, DateTimeKind.Local);

        service.ApplyToolRequest(BuildRequest("begin_keyboard_session", "{\"duration_minutes\":5}"), now);

        var approved = await service.DetermineAutoApproveAsync("type_text", now.AddMinutes(1));
        Assert.True(approved.AutoApprove);
        Assert.True(approved.SessionState.KeyboardSessionActive);

        service.Stop();
        var denied = await service.DetermineAutoApproveAsync("type_text", now.AddMinutes(1));
        Assert.False(denied.AutoApprove);
        Assert.False(denied.SessionState.KeyboardSessionActive);
    }

    [Fact]
    public async Task DetermineAutoApproveAsync_ExpiresSessionsBeforePolicyFallback()
    {
        var service = new ToolControlSessionApplicationService(CreateApprovalService("{\"Enabled\":true,\"AllowedTools\":[\"read_file\"],\"AllowMouseTools\":false,\"AllowKeyboardTools\":false}"));
        var now = new DateTime(2026, 4, 9, 12, 0, 0, DateTimeKind.Local);

        service.ApplyToolRequest(BuildRequest("begin_mouse_session", "{\"duration_minutes\":1}"), now);
        var decision = await service.DetermineAutoApproveAsync("click", now.AddMinutes(2));

        Assert.False(decision.AutoApprove);
        Assert.False(decision.SessionState.MouseSessionActive);
        Assert.Equal("Mouse session expired.", decision.SessionStatusMessage);
    }

    private static ToolApprovalApplicationService CreateApprovalService(string json)
        => new(new ToolAutoAcceptPolicyService(() => Task.FromResult<string?>(json)));

    private static ToolCallRequest BuildRequest(string tool, string json)
        => new()
        {
            Tool = tool,
            Parameters = JsonDocument.Parse(json).RootElement.Clone()
        };
}
