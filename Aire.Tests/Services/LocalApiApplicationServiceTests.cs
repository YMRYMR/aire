using System.Collections.Generic;
using System.Text.Json;
using Aire.AppLayer.Api;
using Aire.Data;
using Aire.Services;
using Xunit;

namespace Aire.Tests.Services;

public sealed class LocalApiApplicationServiceTests
{
    [Fact]
    public void BuildProviderSnapshots_MapsAllProviderFields()
    {
        var service = new LocalApiApplicationService();
        var providers = new[]
        {
            new Provider
            {
                Id = 11,
                Name = "Claude Code",
                Type = "ClaudeCode",
                Model = "claude-sonnet-4-20250514",
                IsEnabled = true,
                Color = "#123456"
            }
        };

        List<ApiProviderSnapshot> snapshots = service.BuildProviderSnapshots(providers);

        Assert.Single(snapshots);
        ApiProviderSnapshot snapshot = snapshots[0];
        Assert.Equal(11, snapshot.Id);
        Assert.Equal("Claude Code", snapshot.Name);
        Assert.Equal("ClaudeCode", snapshot.Type);
        Assert.Equal("Claude Code", snapshot.DisplayType);
        Assert.Equal("claude-sonnet-4-20250514", snapshot.Model);
        Assert.True(snapshot.IsEnabled);
        Assert.Equal("#123456", snapshot.Color);
    }

    [Fact]
    public void BuildConversationCreationPlan_TrimsRequestedTitle_AndUsesDefaultWhenBlank()
    {
        var service = new LocalApiApplicationService();

        LocalApiApplicationService.ConversationCreationPlan trimmed = service.BuildConversationCreationPlan("Mistral", "  Fresh Chat  ");
        LocalApiApplicationService.ConversationCreationPlan fallback = service.BuildConversationCreationPlan("Mistral", "   ");

        Assert.Equal("Fresh Chat", trimmed.Title);
        Assert.Equal("New conversation started with Mistral.", trimmed.SystemMessage);
        Assert.Equal("New Chat", fallback.Title);
        Assert.Equal("New conversation started with Mistral.", fallback.SystemMessage);
    }

    [Fact]
    public void BuildToolRequest_NormalizesToolName_AndClonesParameters()
    {
        var service = new LocalApiApplicationService();
        using JsonDocument document = JsonDocument.Parse("""{"path":"C:/repo","nested":{"value":1}}""");

        ToolCallRequest request = service.BuildToolRequest("notify", document.RootElement);

        Assert.Equal("show_notification", request.Tool);
        Assert.Equal("show_notification", request.Description);
        Assert.Equal(JsonValueKind.Object, request.Parameters.ValueKind);
        Assert.Equal("C:/repo", request.Parameters.GetProperty("path").GetString());
        Assert.Equal(1, request.Parameters.GetProperty("nested").GetProperty("value").GetInt32());
        Assert.Contains("\"tool\":\"show_notification\"", request.RawJson);
        Assert.Contains("\"path\":\"C:/repo\"", request.RawJson);
    }

    [Fact]
    public void BuildStateSnapshot_CarriesRuntimeStateAndProviderInfo()
    {
        var service = new LocalApiApplicationService();
        var provider = new Provider
        {
            Id = 17,
            Name = "qwen2.5:7b",
            Model = "qwen2.5:7b"
        };

        ApiStateSnapshot snapshot = service.BuildStateSnapshot(
            localApiPort: 8123,
            isStartupReady: true,
            isMainWindowVisible: false,
            isSettingsOpen: true,
            isBrowserOpen: false,
            apiAccessEnabled: true,
            hasApiAccessToken: true,
            currentConversationId: 42,
            provider: provider,
            pendingApprovals: 3);

        Assert.Equal(8123, snapshot.LocalApiPort);
        Assert.True(snapshot.IsStartupReady);
        Assert.False(snapshot.IsMainWindowVisible);
        Assert.True(snapshot.IsSettingsOpen);
        Assert.False(snapshot.IsBrowserOpen);
        Assert.True(snapshot.ApiAccessEnabled);
        Assert.True(snapshot.HasApiAccessToken);
        Assert.Equal(42, snapshot.CurrentConversationId);
        Assert.Equal(17, snapshot.CurrentProviderId);
        Assert.Equal("qwen2.5:7b", snapshot.CurrentProviderName);
        Assert.Equal("qwen2.5:7b", snapshot.CurrentProviderModel);
        Assert.Equal(3, snapshot.PendingApprovals);
    }
}
