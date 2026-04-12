using System.Text.Json;
using Aire.AppLayer.Abstractions;
using Aire.AppLayer.Chat;
using Aire.Data;
using Aire.Services;
using Aire.Services.Workflows;
using Xunit;

namespace Aire.Tests.Services;

public sealed class ChatTurnApplicationServiceTests
{
    private readonly StubConversationRepository _conversations = new();
    private readonly StubSettingsRepository _settings = new();
    private readonly ChatSessionApplicationService _chatSessionService;
    private readonly ToolExecutionWorkflowService _toolExecutionWorkflow;

    public ChatTurnApplicationServiceTests()
    {
        _chatSessionService = new ChatSessionApplicationService(_conversations, _settings);
        // ToolExecutionWorkflowService needs a real ToolExecutionService (non-virtual),
        // so we construct one with real file/command services. For our denied-path tests
        // the tool is never actually executed, and for approved paths the stub file content
        // is enough to avoid I/O.
        var toolExecutionService = new Aire.Services.ToolExecutionService(
            new Aire.Services.FileSystemService(), new Aire.Services.CommandExecutionService());
        _toolExecutionWorkflow = new ToolExecutionWorkflowService(
            toolExecutionService, _conversations, _settings);
    }

    private ChatTurnApplicationService CreateService()
        => new(_chatSessionService, _toolExecutionWorkflow);

    // ── HandleAttemptCompletion ─────────────────────────────────────────

    [Fact]
    public void HandleAttemptCompletion_WithResultText_ReturnsCompletionResult()
    {
        var service = CreateService();
        var request = MakeAttemptCompletion("""{"result":"The task is complete."}""");

        var result = service.HandleAttemptCompletion(request);

        Assert.NotNull(result);
        Assert.Equal("The task is complete.", result!.FinalText);
        Assert.Equal("assistant", result.AssistantHistoryMessage.Role);
        Assert.Equal("The task is complete.", result.AssistantHistoryMessage.Content);
    }

    [Fact]
    public void HandleAttemptCompletion_WithEmptyResult_ReturnsNull()
    {
        var service = CreateService();
        var request = MakeAttemptCompletion("""{"result":""}""");

        var result = service.HandleAttemptCompletion(request);

        Assert.Null(result);
    }

    [Fact]
    public void HandleAttemptCompletion_WithWhitespaceResult_ReturnsNull()
    {
        var service = CreateService();
        var request = MakeAttemptCompletion("""{"result":"   "}""");

        var result = service.HandleAttemptCompletion(request);

        Assert.Null(result);
    }

    [Fact]
    public void HandleAttemptCompletion_WithNoResultProperty_ReturnsNull()
    {
        var service = CreateService();
        var request = MakeAttemptCompletion("""{"other":"data"}""");

        var result = service.HandleAttemptCompletion(request);

        Assert.Null(result);
    }

    [Fact]
    public void HandleAttemptCompletion_WithNullResultValue_ReturnsNull()
    {
        var service = CreateService();
        var request = MakeAttemptCompletion("""{"result":null}""");

        var result = service.HandleAttemptCompletion(request);

        Assert.Null(result);
    }

    // ── HandleSuccessTextAsync ──────────────────────────────────────────

    [Fact]
    public async Task HandleSuccessTextAsync_WithVisibleWindow_TrailerPreviewIsNull()
    {
        var service = CreateService();
        _conversations.AddConversation(1, 10, "Test");

        var result = await service.HandleSuccessTextAsync(
            "Hello world", conversationId: 1, tokensUsed: null, isWindowVisible: true);

        Assert.NotNull(result);
        Assert.Equal("Hello world", result.FinalText);
        Assert.Null(result.TrayPreview);
    }

    [Fact]
    public async Task HandleSuccessTextAsync_WithHiddenWindow_TrailerPreviewIsGenerated()
    {
        var service = CreateService();
        _conversations.AddConversation(1, 10, "Test");

        var result = await service.HandleSuccessTextAsync(
            "Hello world", conversationId: 1, tokensUsed: null, isWindowVisible: false);

        Assert.NotNull(result);
        Assert.Equal("Hello world", result.FinalText);
        Assert.NotNull(result.TrayPreview);
        Assert.Equal("Hello world", result.TrayPreview);
    }

    [Fact]
    public async Task HandleSuccessTextAsync_LongText_TruncatesTrayPreview()
    {
        var service = CreateService();
        _conversations.AddConversation(1, 10, "Test");
        var longText = new string('a', 200);

        var result = await service.HandleSuccessTextAsync(
            longText, conversationId: 1, tokensUsed: null, isWindowVisible: false, trayPreviewLength: 80);

        Assert.NotNull(result);
        Assert.True(result.TrayPreview!.Length <= 81);
        Assert.EndsWith("\u2026", result.TrayPreview);
    }

    [Fact]
    public async Task HandleSuccessTextAsync_EmptyText_ReturnsEmptyResponsePlaceholder()
    {
        var service = CreateService();

        var result = await service.HandleSuccessTextAsync(
            "", conversationId: null, tokensUsed: null, isWindowVisible: true);

        Assert.NotNull(result);
        Assert.Equal("(empty response)", result.FinalText);
    }

    [Fact]
    public async Task HandleSuccessTextAsync_NullConversation_DoesNotPersist()
    {
        var service = CreateService();

        var result = await service.HandleSuccessTextAsync(
            "Hello", conversationId: null, tokensUsed: null, isWindowVisible: true);

        Assert.NotNull(result);
        Assert.Equal("Hello", result.FinalText);
        Assert.Empty(_conversations.SavedMessages);
    }

    [Fact]
    public async Task HandleSuccessTextAsync_WithConversationId_PersistsMessage()
    {
        var service = CreateService();
        _conversations.AddConversation(5, 10, "Test");

        var result = await service.HandleSuccessTextAsync(
            "Hello persisted", conversationId: 5, tokensUsed: 42, isWindowVisible: true);

        Assert.NotNull(result);
        Assert.Equal("Hello persisted", result.FinalText);
        Assert.Single(_conversations.SavedMessages);
        Assert.Equal("assistant", _conversations.SavedMessages[0].Role);
        Assert.Equal("Hello persisted", _conversations.SavedMessages[0].Content);
    }

    [Fact]
    public async Task HandleSuccessTextAsync_AssistantHistoryMessage_HasAssistantRole()
    {
        var service = CreateService();

        var result = await service.HandleSuccessTextAsync(
            "test content", conversationId: null, tokensUsed: null, isWindowVisible: true);

        Assert.NotNull(result);
        Assert.Equal("assistant", result.AssistantHistoryMessage.Role);
        Assert.Equal("test content", result.AssistantHistoryMessage.Content);
    }

    // ── PersistAssistantMessageAsync ────────────────────────────────────

    [Fact]
    public async Task PersistAssistantMessageAsync_DelegatesToSessionService()
    {
        var service = CreateService();
        _conversations.AddConversation(3, 10, "Test");

        await service.PersistAssistantMessageAsync(3, "direct message");

        Assert.Single(_conversations.SavedMessages);
        Assert.Equal("assistant", _conversations.SavedMessages[0].Role);
        Assert.Equal("direct message", _conversations.SavedMessages[0].Content);
    }

    // ── HandleToolExecutionAsync (denied path) ─────────────────────────

    [Fact]
    public async Task HandleToolExecutionAsync_Denied_SetsApprovedFalse()
    {
        var service = CreateService();
        var toolCall = new ToolCallRequest
        {
            Tool = "read_file",
            Description = "Read a file",
            RawJson = """{"tool":"read_file","parameters":{"path":"C:/test.txt"}}""",
            Parameters = JsonDocument.Parse("""{"path":"C:/test.txt"}""").RootElement
        };

        var result = await service.HandleToolExecutionAsync(
            "Here is the file", toolCall, approved: false, conversationId: null,
            includeScreenshotImageInHistory: false);

        Assert.False(result.ExecutionOutcome.Approved);
    }

    [Fact]
    public async Task HandleToolExecutionAsync_Denied_ToolResultContainsDenial()
    {
        var service = CreateService();
        var toolCall = new ToolCallRequest
        {
            Tool = "read_file",
            Description = "Read a file",
            RawJson = """{"tool":"read_file","parameters":{"path":"C:/test.txt"}}""",
            Parameters = JsonDocument.Parse("""{"path":"C:/test.txt"}""").RootElement
        };

        var result = await service.HandleToolExecutionAsync(
            "Here is the file", toolCall, approved: false, conversationId: null,
            includeScreenshotImageInHistory: false);

        Assert.Contains("denied", result.ToolResult.ToLowerInvariant());
    }

    [Fact]
    public async Task HandleToolExecutionAsync_AssistantHistoryMessage_HasAssistantRole()
    {
        var service = CreateService();
        var toolCall = new ToolCallRequest
        {
            Tool = "read_file",
            Description = "Read",
            RawJson = """{"tool":"read_file"}""",
            Parameters = JsonDocument.Parse("""{"path":"C:/x"}""").RootElement
        };

        var result = await service.HandleToolExecutionAsync(
            "Reading file", toolCall, approved: false, conversationId: null,
            includeScreenshotImageInHistory: false);

        Assert.Equal("assistant", result.AssistantHistoryMessage.Role);
    }

    [Fact]
    public async Task HandleToolExecutionAsync_ToolHistoryMessage_HasUserRole()
    {
        var service = CreateService();
        var toolCall = new ToolCallRequest
        {
            Tool = "read_file",
            Description = "Read",
            RawJson = """{"tool":"read_file"}""",
            Parameters = JsonDocument.Parse("""{"path":"C:/x"}""").RootElement
        };

        var result = await service.HandleToolExecutionAsync(
            "Reading", toolCall, approved: false, conversationId: null,
            includeScreenshotImageInHistory: false);

        Assert.Equal("user", result.ToolHistoryMessage.Role);
    }

    [Fact]
    public async Task HandleToolExecutionAsync_ParsedOverload_DelegatesCorrectly()
    {
        var service = CreateService();
        var parsed = new ParsedAiResponse
        {
            TextContent = "Let me look",
            ToolCall = new ToolCallRequest
            {
                Tool = "read_file",
                Description = "Read",
                RawJson = """{"tool":"read_file","parameters":{"path":"C:/a.txt"}}""",
                Parameters = JsonDocument.Parse("""{"path":"C:/a.txt"}""").RootElement
            }
        };

        var result = await service.HandleToolExecutionAsync(
            parsed, approved: false, conversationId: null,
            includeScreenshotImageInHistory: false);

        Assert.False(result.ExecutionOutcome.Approved);
    }

    [Fact]
    public async Task HandleToolExecutionAsync_BuildsAssistantToolCallContent_WithText()
    {
        var service = CreateService();
        var rawJson = """{"tool":"read_file","parameters":{"path":"C:/test.txt"}}""";
        var toolCall = new ToolCallRequest
        {
            Tool = "read_file",
            Description = "Read a file",
            RawJson = rawJson,
            Parameters = JsonDocument.Parse("""{"path":"C:/test.txt"}""").RootElement
        };

        var result = await service.HandleToolExecutionAsync(
            "Let me read", toolCall, approved: false, conversationId: null,
            includeScreenshotImageInHistory: false);

        Assert.Contains("Let me read", result.AssistantToolCallContent);
        Assert.Contains(rawJson, result.AssistantToolCallContent);
    }

    [Fact]
    public async Task HandleToolExecutionAsync_BuildsAssistantToolCallContent_WithoutText()
    {
        var service = CreateService();
        var rawJson = """{"tool":"list_files","parameters":{"directory":"C:/"}}""";
        var toolCall = new ToolCallRequest
        {
            Tool = "list_files",
            Description = "List files",
            RawJson = rawJson,
            Parameters = JsonDocument.Parse("""{"directory":"C:/"}""").RootElement
        };

        var result = await service.HandleToolExecutionAsync(
            "", toolCall, approved: false, conversationId: null,
            includeScreenshotImageInHistory: false);

        Assert.Contains(rawJson, result.AssistantToolCallContent);
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private static ToolCallRequest MakeAttemptCompletion(string parametersJson)
        => new()
        {
            Tool = "attempt_completion",
            Parameters = JsonDocument.Parse(parametersJson).RootElement
        };

    // ── Stub dependencies ───────────────────────────────────────────────

    private sealed class StubConversationRepository : IConversationRepository
    {
        private readonly Dictionary<int, Aire.Data.Conversation> _conversations = [];
        private readonly List<Aire.Data.Message> _savedMessages = [];

        public List<Aire.Data.Message> SavedMessages => _savedMessages;

        public void AddConversation(int id, int providerId, string title)
            => _conversations[id] = new Aire.Data.Conversation { Id = id, ProviderId = providerId, Title = title };

        public Task<int> CreateConversationAsync(int providerId, string title)
        {
            var id = _conversations.Count + 1;
            AddConversation(id, providerId, title);
            return Task.FromResult(id);
        }

        public Task<Aire.Data.Conversation?> GetLatestConversationAsync(int providerId)
            => Task.FromResult(_conversations.Values.LastOrDefault(c => c.ProviderId == providerId));

        public Task<Aire.Data.Conversation?> GetConversationAsync(int conversationId)
            => Task.FromResult(_conversations.GetValueOrDefault(conversationId));

        public Task<List<ConversationSummary>> ListConversationsAsync(string? search = null)
            => Task.FromResult(new List<ConversationSummary>());

        public Task UpdateConversationTitleAsync(int conversationId, string title)
            => Task.CompletedTask;

        public Task UpdateConversationProviderAsync(int conversationId, int providerId)
            => Task.CompletedTask;

        public Task UpdateConversationAssistantModeAsync(int conversationId, string assistantModeKey)
            => Task.CompletedTask;

        public Task SaveMessageAsync(int conversationId, string role, string content,
            string? imagePath = null, IEnumerable<MessageAttachment>? attachments = null, int? tokens = null)
        {
            _savedMessages.Add(new Aire.Data.Message
            {
                ConversationId = conversationId,
                Role = role,
                Content = content
            });
            return Task.CompletedTask;
        }

        public Task<List<Aire.Data.Message>> GetMessagesAsync(int conversationId)
            => Task.FromResult(_savedMessages.FindAll(m => m.ConversationId == conversationId));

        public Task DeleteMessagesByConversationIdAsync(int conversationId)
        {
            _savedMessages.RemoveAll(m => m.ConversationId == conversationId);
            return Task.CompletedTask;
        }

        public Task DeleteConversationAsync(int conversationId)
        {
            _conversations.Remove(conversationId);
            return Task.CompletedTask;
        }

        public Task DeleteAllConversationsAsync()
        {
            _conversations.Clear();
            _savedMessages.Clear();
            return Task.CompletedTask;
        }
    }

    private sealed class StubSettingsRepository : ISettingsRepository
    {
        public Task<string?> GetSettingAsync(string key) => Task.FromResult((string?)null);
        public Task SetSettingAsync(string key, string value) => Task.CompletedTask;
        public Task LogFileAccessAsync(string operation, string path, bool allowed) => Task.CompletedTask;
    }
}
