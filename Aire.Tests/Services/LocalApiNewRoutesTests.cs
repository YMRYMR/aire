using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Aire.Data;
using Aire.Services;
using Xunit;

namespace Aire.Tests.Services;

/// <summary>
/// Tests for the API routes and helper methods added as part of the "full API parity" initiative.
/// Covers: GetNullableBool, all new dispatch routes, and toggle-semantics for the new handlers.
/// </summary>
public sealed class LocalApiNewRoutesTests
{
    // ── Helper: GetNullableBool ─────────────────────────────────────────────

    [Fact]
    public void GetNullableBool_ReturnsTrueOrFalse_ForBooleanLiterals()
    {
        using var doc = JsonDocument.Parse("""{"enabled":true,"disabled":false}""");

        Assert.True(LocalApiService.GetNullableBool(doc.RootElement, "enabled"));
        Assert.False(LocalApiService.GetNullableBool(doc.RootElement, "disabled"));
    }

    [Fact]
    public void GetNullableBool_ParsesStringRepresentations()
    {
        using var doc = JsonDocument.Parse("""{"a":"True","b":"false","c":"yes"}""");

        Assert.True(LocalApiService.GetNullableBool(doc.RootElement, "a"));
        Assert.False(LocalApiService.GetNullableBool(doc.RootElement, "b"));
        Assert.Null(LocalApiService.GetNullableBool(doc.RootElement, "c"));
    }

    [Fact]
    public void GetNullableBool_ReturnsNull_WhenPropertyIsMissingOrNull()
    {
        using var doc = JsonDocument.Parse("""{"x":null}""");

        Assert.Null(LocalApiService.GetNullableBool(doc.RootElement, "x"));
        Assert.Null(LocalApiService.GetNullableBool(doc.RootElement, "missing"));
        Assert.Null(LocalApiService.GetNullableBool(null, "missing"));
    }

    // ── Helper: All dispatch routes are wired up (no "Unknown method" error) ─

    [Theory]
    [InlineData("toggle_sidebar")]
    [InlineData("open_sidebar")]
    [InlineData("close_sidebar")]
    [InlineData("toggle_voice_output")]
    [InlineData("open_search")]
    [InlineData("set_search")]
    [InlineData("navigate_search")]
    [InlineData("search_next")]
    [InlineData("search_prev")]
    [InlineData("close_search")]
    [InlineData("pin_window")]
    [InlineData("toggle_agent_mode")]
    [InlineData("start_agent_mode")]
    [InlineData("stop_agent_mode")]
    [InlineData("set_input")]
    [InlineData("attach_file")]
    [InlineData("remove_attachment")]
    [InlineData("branch_from_message")]
    public async Task NewDispatchRoute_IsRecognized_AndCallsHandler(string method)
    {
        var stub = new StubApiCommandHandler();
        var service = new LocalApiService(stub);

        var parameters = method switch
        {
            "navigate_search" => JsonDocument.Parse("""{"direction":"next"}""").RootElement.Clone(),
            "set_input" => JsonDocument.Parse("""{"text":"hello"}""").RootElement.Clone(),
            "attach_file" => JsonDocument.Parse("""{"filePath":"C:/nonexistent.txt"}""").RootElement.Clone(),
            "branch_from_message" => JsonDocument.Parse("""{"messageId":1}""").RootElement.Clone(),
            _ => (JsonElement?)null
        };

        var request = new LocalApiRequest
        {
            Method = method,
            Parameters = parameters
        };

        var response = await service.DispatchAsync(request, CancellationToken.None);

        Assert.DoesNotContain("Unknown method", response.ErrorMessage ?? string.Empty, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains(NormalizeMethod(method), stub.CalledMethods);
    }

    // ── Routing semantics: open/close_sidebar map to toggle_sidebar ──────────

    [Fact]
    public async Task OpenSidebarRoute_PassesTrue_ToHandler()
    {
        var stub = new StubApiCommandHandler();
        var service = new LocalApiService(stub);

        await service.DispatchAsync(new LocalApiRequest { Method = "open_sidebar" }, CancellationToken.None);

        Assert.True(stub.LastSidebarOpen);
    }

    [Fact]
    public async Task CloseSidebarRoute_PassesFalse_ToHandler()
    {
        var stub = new StubApiCommandHandler();
        var service = new LocalApiService(stub);

        await service.DispatchAsync(new LocalApiRequest { Method = "close_sidebar" }, CancellationToken.None);

        Assert.False(stub.LastSidebarOpen);
    }

    [Fact]
    public async Task ToggleSidebarRoute_WithOpenTrue_PassesTrue()
    {
        var stub = new StubApiCommandHandler();
        var service = new LocalApiService(stub);

        await service.DispatchAsync(new LocalApiRequest
        {
            Method = "toggle_sidebar",
            Parameters = JsonDocument.Parse("""{"open":true}""").RootElement.Clone()
        }, CancellationToken.None);

        Assert.True(stub.LastSidebarOpen);
    }

    [Fact]
    public async Task ToggleSidebarRoute_WithNoParam_PassesNull()
    {
        var stub = new StubApiCommandHandler();
        var service = new LocalApiService(stub);

        await service.DispatchAsync(new LocalApiRequest { Method = "toggle_sidebar" }, CancellationToken.None);

        Assert.Null(stub.LastSidebarOpen);
    }

    // ── Routing semantics: search routes ─────────────────────────────────────

    [Fact]
    public async Task SearchNextRoute_PassesNextDirection()
    {
        var stub = new StubApiCommandHandler();
        var service = new LocalApiService(stub);

        await service.DispatchAsync(new LocalApiRequest { Method = "search_next" }, CancellationToken.None);

        Assert.Equal("next", stub.LastSearchDirection);
    }

    [Fact]
    public async Task SearchPrevRoute_PassesPrevDirection()
    {
        var stub = new StubApiCommandHandler();
        var service = new LocalApiService(stub);

        await service.DispatchAsync(new LocalApiRequest { Method = "search_prev" }, CancellationToken.None);

        Assert.Equal("prev", stub.LastSearchDirection);
    }

    [Fact]
    public async Task NavigateSearchRoute_PassesExplicitDirection()
    {
        var stub = new StubApiCommandHandler();
        var service = new LocalApiService(stub);

        await service.DispatchAsync(new LocalApiRequest
        {
            Method = "navigate_search",
            Parameters = JsonDocument.Parse("""{"direction":"prev"}""").RootElement.Clone()
        }, CancellationToken.None);

        Assert.Equal("prev", stub.LastSearchDirection);
    }

    // ── Routing semantics: agent mode ────────────────────────────────────────

    [Fact]
    public async Task StartAgentModeRoute_PassesTrueWithBudget()
    {
        var stub = new StubApiCommandHandler();
        var service = new LocalApiService(stub);

        await service.DispatchAsync(new LocalApiRequest
        {
            Method = "start_agent_mode",
            Parameters = JsonDocument.Parse("""{"budget":5000,"categories":["files","shell"]}""").RootElement.Clone()
        }, CancellationToken.None);

        Assert.True(stub.LastAgentEnabled);
        Assert.Equal(5000, stub.LastAgentBudget);
        Assert.Contains("files", stub.LastAgentCategories!);
        Assert.Contains("shell", stub.LastAgentCategories!);
    }

    [Fact]
    public async Task StopAgentModeRoute_PassesFalse()
    {
        var stub = new StubApiCommandHandler();
        var service = new LocalApiService(stub);

        await service.DispatchAsync(new LocalApiRequest { Method = "stop_agent_mode" }, CancellationToken.None);

        Assert.False(stub.LastAgentEnabled);
    }

    // ── Routing semantics: composer ──────────────────────────────────────────

    [Fact]
    public async Task SetInputRoute_PassesTextToHandler()
    {
        var stub = new StubApiCommandHandler();
        var service = new LocalApiService(stub);

        await service.DispatchAsync(new LocalApiRequest
        {
            Method = "set_input",
            Parameters = JsonDocument.Parse("""{"text":"Draft message"}""").RootElement.Clone()
        }, CancellationToken.None);

        Assert.Equal("Draft message", stub.LastSetInputText);
    }

    [Fact]
    public async Task AttachFileRoute_PassesFilePathToHandler()
    {
        var stub = new StubApiCommandHandler();
        var service = new LocalApiService(stub);

        await service.DispatchAsync(new LocalApiRequest
        {
            Method = "attach_file",
            Parameters = JsonDocument.Parse("""{"filePath":"C:/docs/report.pdf"}""").RootElement.Clone()
        }, CancellationToken.None);

        Assert.Equal("C:/docs/report.pdf", stub.LastAttachFilePath);
    }

    [Fact]
    public async Task RemoveAttachmentRoute_CallsHandler()
    {
        var stub = new StubApiCommandHandler();
        var service = new LocalApiService(stub);

        await service.DispatchAsync(new LocalApiRequest { Method = "remove_attachment" }, CancellationToken.None);

        Assert.True(stub.RemoveAttachmentCalled);
    }

    // ── Routing semantics: branching ─────────────────────────────────────────

    [Fact]
    public async Task BranchFromMessageRoute_PassesMessageId()
    {
        var stub = new StubApiCommandHandler();
        var service = new LocalApiService(stub);

        await service.DispatchAsync(new LocalApiRequest
        {
            Method = "branch_from_message",
            Parameters = JsonDocument.Parse("""{"messageId":77}""").RootElement.Clone()
        }, CancellationToken.None);

        Assert.Equal(77, stub.LastBranchMessageId);
    }

    // ── Helper ───────────────────────────────────────────────────────────────

    private static string NormalizeMethod(string method) => method switch
    {
        "open_sidebar" or "close_sidebar" or "toggle_sidebar" => "toggle_sidebar",
        "search_next" or "search_prev" or "navigate_search" => "navigate_search",
        "set_search" or "open_search" => "open_search",
        "start_agent_mode" or "stop_agent_mode" or "toggle_agent_mode" => "toggle_agent_mode",
        _ => method
    };

    // ── Error codes ─────────────────────────────────────────────────────────

    [Fact]
    public async Task UnknownMethod_ReturnsErrorCode()
    {
        var stub = new StubApiCommandHandler();
        var service = new LocalApiService(stub);

        var response = await service.DispatchAsync(
            new LocalApiRequest { Method = "nonexistent_xyz" }, CancellationToken.None);

        Assert.False(response.Ok);
        Assert.Equal("UNKNOWN_METHOD", response.ErrorCode);
    }

    [Fact]
    public async Task InvalidOperation_ReturnsInvalidParamsCode()
    {
        var stub = new StubApiCommandHandler();
        var service = new LocalApiService(stub);

        // select_window requires window selection parameters — will throw InvalidOperationException
        var response = await service.DispatchAsync(
            new LocalApiRequest { Method = "select_window" }, CancellationToken.None);

        Assert.False(response.Ok);
        Assert.Equal("INVALID_PARAMS", response.ErrorCode);
    }

    // ── Method catalog ─────────────────────────────────────────────────────

    [Fact]
    public async Task ListMethods_ReturnsCatalog()
    {
        var stub = new StubApiCommandHandler();
        var service = new LocalApiService(stub);

        var response = await service.DispatchAsync(
            new LocalApiRequest { Method = "list_methods" }, CancellationToken.None);

        Assert.True(response.Ok);
        Assert.NotNull(response.Result);

        var json = JsonSerializer.Serialize(response.Result);
        using var doc = JsonDocument.Parse(json);
        var catalog = doc.RootElement;

        Assert.Equal(JsonValueKind.Array, catalog.ValueKind);
        Assert.True(catalog.GetArrayLength() > 50);

        // Verify first entry shape
        var first = catalog[0];
        Assert.True(first.TryGetProperty("method", out _));
        Assert.True(first.TryGetProperty("description", out _));
        Assert.True(first.TryGetProperty("parameters", out _));
    }

    [Fact]
    public async Task ListMethods_ContainsKeyMethods()
    {
        var stub = new StubApiCommandHandler();
        var service = new LocalApiService(stub);

        var response = await service.DispatchAsync(
            new LocalApiRequest { Method = "list_methods" }, CancellationToken.None);

        var json = JsonSerializer.Serialize(response.Result);
        using var doc = JsonDocument.Parse(json);

        var methods = doc.RootElement.EnumerateArray()
            .Select(e => e.GetProperty("method").GetString())
            .ToHashSet();

        Assert.Contains("send_message", methods);
        Assert.Contains("list_methods", methods);
        Assert.Contains("toggle_orchestrator_mode", methods);
    }

    // ── Stub handler ─────────────────────────────────────────────────────────

    private sealed class StubApiCommandHandler : IApiCommandHandler
    {
        public List<string> CalledMethods { get; } = new();

        // Recorded call arguments
        public bool? LastSidebarOpen { get; private set; } = null;
        public bool? LastVoiceOutputEnabled { get; private set; } = null;
        public string? LastSearchQuery { get; private set; } = null;
        public string? LastSearchDirection { get; private set; } = null;
        public bool? LastAgentEnabled { get; private set; } = null;
        public int? LastAgentBudget { get; private set; } = null;
        public List<string>? LastAgentCategories { get; private set; } = null;
        public bool? LastPinned { get; private set; } = null;
        public string? LastSetInputText { get; private set; } = null;
        public string? LastAttachFilePath { get; private set; } = null;
        public bool RemoveAttachmentCalled { get; private set; } = false;
        public int LastBranchMessageId { get; private set; } = -1;

        // ── IApiCommandHandler members that are always needed ────────────────
        public Task<ApiStateSnapshot> ApiGetStateAsync()
        {
            CalledMethods.Add("get_state");
            return Task.FromResult(new ApiStateSnapshot());
        }

        public Task ShowMainWindowAsync() { CalledMethods.Add("show_main_window"); return Task.CompletedTask; }
        public Task HideMainWindowAsync() { CalledMethods.Add("hide_main_window"); return Task.CompletedTask; }
        public Task ShowSettingsWindowAsync(string? tab = null) { CalledMethods.Add("open_settings"); return Task.CompletedTask; }
        public Task ShowBrowserWindowAsync() { CalledMethods.Add("open_browser"); return Task.CompletedTask; }

        public Task<List<ApiProviderSnapshot>> ApiListProvidersAsync()
        {
            CalledMethods.Add("list_providers");
            return Task.FromResult(new List<ApiProviderSnapshot>());
        }

        public Task<ApiProviderSnapshot> ApiCreateProviderAsync(
            string? name, string type, string? apiKey, string? baseUrl, string model,
            bool isEnabled, string? color, bool selectAfterCreate, int? inheritCredentialsFromProviderId)
        {
            CalledMethods.Add("create_provider");
            return Task.FromResult(new ApiProviderSnapshot { Name = name ?? "" });
        }

        public Task<List<ConversationSummary>> ApiListConversationsAsync(string? search)
        {
            CalledMethods.Add("list_conversations");
            return Task.FromResult(new List<ConversationSummary>());
        }

        public Task<int> ApiCreateConversationAsync(string? title, int? providerId)
        {
            CalledMethods.Add("create_conversation");
            return Task.FromResult(1);
        }

        public Task<bool> ApiSelectConversationAsync(int conversationId)
        {
            CalledMethods.Add("select_conversation");
            return Task.FromResult(true);
        }

        public Task<bool> ApiDeleteConversationAsync(int conversationId)
        {
            CalledMethods.Add("delete_conversation");
            return Task.FromResult(true);
        }

        public Task<bool> ApiRenameConversationAsync(int conversationId, string title)
        {
            CalledMethods.Add("rename_conversation");
            return Task.FromResult(true);
        }

        public Task<List<Message>> ApiGetMessagesAsync(int conversationId)
        {
            CalledMethods.Add("get_messages");
            return Task.FromResult(new List<Message>());
        }

        public Task<bool> ApiSetProviderAsync(int providerId)
        {
            CalledMethods.Add("set_provider");
            return Task.FromResult(true);
        }

        public Task<bool> ApiSetProviderModelAsync(int providerId, string model)
        {
            CalledMethods.Add("set_provider_model");
            return Task.FromResult(true);
        }

        public Task<bool> ApiSendMessageAsync(string text, int? conversationId)
        {
            CalledMethods.Add("send_message");
            return Task.FromResult(true);
        }

        public Task<bool> ApiSetAssistantModeAsync(string modeKey)
        {
            CalledMethods.Add("set_assistant_mode");
            return Task.FromResult(true);
        }

        public Task<List<string>> ApiListAssistantModesAsync()
        {
            CalledMethods.Add("list_assistant_modes");
            return Task.FromResult(new List<string>());
        }

        public Task<List<string>> ApiListToolCategoriesAsync()
        {
            CalledMethods.Add("list_tool_categories");
            return Task.FromResult(new List<string>());
        }

        public Task<bool> ApiSetToolCategoriesAsync(List<string>? categories)
        {
            CalledMethods.Add("set_tool_categories");
            return Task.FromResult(true);
        }

        public Task StopAiAsync()
        {
            CalledMethods.Add("stop_ai");
            return Task.CompletedTask;
        }

        public Task<ApiPendingApproval[]> ApiListPendingApprovalsAsync()
        {
            CalledMethods.Add("list_pending_approvals");
            return Task.FromResult(System.Array.Empty<ApiPendingApproval>());
        }

        public Task<ApiPendingApproval?> ApiGetFirstPendingApprovalAsync()
        {
            CalledMethods.Add("wait_for_pending_approval");
            return Task.FromResult<ApiPendingApproval?>(null);
        }

        public Task<bool> ApiSetPendingApprovalAsync(int index, bool approved)
        {
            CalledMethods.Add(approved ? "approve_tool_call" : "deny_tool_call");
            return Task.FromResult(true);
        }

        public Task<ApiToolExecutionResult> ApiExecuteToolAsync(
            string tool, JsonElement parameters, bool waitForApproval, int approvalTimeoutSeconds)
        {
            CalledMethods.Add("execute_tool");
            return Task.FromResult(new ApiToolExecutionResult { Status = "completed" });
        }

        // ── New UI state control methods ─────────────────────────────────────

        public Task<bool> ApiToggleSidebarAsync(bool? open = null)
        {
            CalledMethods.Add("toggle_sidebar");
            LastSidebarOpen = open;
            return Task.FromResult(true);
        }

        public Task<bool> ApiToggleVoiceOutputAsync(bool? enabled = null)
        {
            CalledMethods.Add("toggle_voice_output");
            LastVoiceOutputEnabled = enabled;
            return Task.FromResult(true);
        }

        public Task<bool> ApiOpenSearchAsync(string? query = null)
        {
            CalledMethods.Add("open_search");
            LastSearchQuery = query;
            return Task.FromResult(true);
        }

        public Task<bool> ApiNavigateSearchAsync(string direction)
        {
            CalledMethods.Add("navigate_search");
            LastSearchDirection = direction;
            return Task.FromResult(true);
        }

        public Task<bool> ApiCloseSearchAsync()
        {
            CalledMethods.Add("close_search");
            return Task.FromResult(true);
        }

        public Task<bool> ApiPinWindowAsync(bool? pinned = null)
        {
            CalledMethods.Add("pin_window");
            LastPinned = pinned;
            return Task.FromResult(true);
        }

        public Task<bool> ApiToggleOrchestratorModeAsync(bool? enabled = null, int? budget = null, List<string>? categories = null, List<string>? goals = null)
        {
            CalledMethods.Add("toggle_orchestrator_mode");
            LastAgentEnabled = enabled;
            LastAgentBudget = budget;
            LastAgentCategories = categories;
            return Task.FromResult(true);
        }

        public Task<bool> ApiSetOrchestratorGoalsAsync(List<string> goals)
        {
            CalledMethods.Add("set_orchestrator_goals");
            return Task.FromResult(true);
        }

        public Task<bool> ApiToggleAgentModeAsync(bool? enabled = null, int? budget = null, List<string>? categories = null)
        {
            CalledMethods.Add("toggle_agent_mode");
            LastAgentEnabled = enabled;
            LastAgentBudget = budget;
            LastAgentCategories = categories;
            return Task.FromResult(true);
        }

        public Task<bool> ApiSetInputAsync(string text)
        {
            CalledMethods.Add("set_input");
            LastSetInputText = text;
            return Task.FromResult(true);
        }

        public Task<bool> ApiAttachFileAsync(string filePath)
        {
            CalledMethods.Add("attach_file");
            LastAttachFilePath = filePath;
            return Task.FromResult(true);
        }

        public Task<bool> ApiRemoveAttachmentAsync()
        {
            CalledMethods.Add("remove_attachment");
            RemoveAttachmentCalled = true;
            return Task.FromResult(true);
        }

        public Task<bool> ApiBranchFromMessageAsync(int messageId)
        {
            CalledMethods.Add("branch_from_message");
            LastBranchMessageId = messageId;
            return Task.FromResult(true);
        }

        public Task<int> ApiBranchConversationAsync(int conversationId, int upToMessageId)
        {
            CalledMethods.Add("branch_conversation");
            return Task.FromResult(2);
        }
    }
}
