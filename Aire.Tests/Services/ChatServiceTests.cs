using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Aire.AppLayer.Abstractions;
using Aire.AppLayer.Providers;
using Aire.Data;
using Aire.Domain.Providers;
using Aire.Providers;
using Aire.Services;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Aire.Tests.Services;

public class ChatServiceTests : IAsyncLifetime, IDisposable
{
    private readonly string _dbPath;
    private readonly DatabaseService _db;
    private readonly ProviderFactory _factory;
    private readonly ChatService _chatService;

    public ChatServiceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"aire_test_{Guid.NewGuid():N}.db");
        _db = new DatabaseService(_dbPath);
        _factory = new ProviderFactory(_db);
        _chatService = new ChatService(_factory);
    }

    public async Task InitializeAsync() => await _db.InitializeAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        _db.Dispose();
        SqliteConnection.ClearAllPools();
        try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { }
    }

    [Fact]
    public async Task SendMessageAsync_WithoutProvider_ThrowsInvalidOperationException()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _chatService.SendMessageAsync("Hello"));
    }

    [Fact]
    public void Constructor_NullProviderFactory_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new ChatService(null));
    }

    [Fact]
    public async Task GetConversationsAsync_ReturnsEmptyList()
    {
        List<Aire.Services.Conversation> conversations = await _chatService.GetConversationsAsync();
        Assert.NotNull(conversations);
        Assert.Empty(conversations);
    }

    [Fact]
    public async Task SetProviderAsync_WithOpenAiProvider_SetsCurrentProvider()
    {
        Provider newProvider = new Provider
        {
            Name = "Test OpenAI",
            Type = "OpenAI",
            ApiKey = "sk-test",
            Model = "gpt-4o",
            IsEnabled = true,
            Color = "#000000"
        };
        int id = await _db.InsertProviderAsync(newProvider);
        Assert.Null(await Record.ExceptionAsync(() => _chatService.SetProviderAsync(id)));
    }

    [Fact]
    public async Task SendMessageWithHistoryAsync_WithoutProvider_ThrowsInvalidOperationException()
    {
        List<ChatMessage> messages = new List<ChatMessage>
        {
            new ChatMessage
            {
                Role = "user",
                Content = "Hello"
            }
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() => _chatService.SendMessageWithHistoryAsync(messages));
    }

    [Fact]
    public async Task StreamMessageAsync_WithoutProvider_ThrowsInvalidOperationException()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _chatService.StreamMessageAsync("Hello"));
    }

    [Fact]
    public async Task SaveMessageAsync_CompletesAsCompatibilityNoOp()
    {
        Assert.Null(await Record.ExceptionAsync(() => _chatService.SaveMessageAsync(1, "user", "hello", "image.png")));
    }

    [Fact]
    public void Conversation_StoresAssignedValues()
    {
        DateTime utcNow = DateTime.UtcNow;
        Aire.Services.Conversation conversation = new Aire.Services.Conversation
        {
            Id = 7,
            ProviderId = 3,
            Title = "Title",
            CreatedAt = utcNow,
            UpdatedAt = utcNow.AddMinutes(1.0)
        };
        Assert.Equal(7, conversation.Id);
        Assert.Equal(3, conversation.ProviderId);
        Assert.Equal("Title", conversation.Title);
        Assert.Equal(utcNow, conversation.CreatedAt);
        Assert.Equal(utcNow.AddMinutes(1.0), conversation.UpdatedAt);
    }

    [Fact]
    public async Task SendMessageWithHistoryAsync_UsesRuntimeWorkflowExecutionPath()
    {
        var adapter = new RecordingAdapter();
        var runtimeWorkflow = new ProviderRuntimeApplicationService(new ProviderAdapterApplicationService([adapter]));
        var service = new ChatService(_factory, runtimeWorkflow, new ChatOrchestrator());
        var provider = new FakeProvider();
        typeof(ChatService)
            .GetField("_currentProvider", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(service, provider);

        AiResponse? completed = null;
        service.ResponseCompleted += (_, response) => completed = response;

        var response = await service.SendMessageWithHistoryAsync(
        [
            new ChatMessage
            {
                Role = "user",
                Content = "hello"
            }
        ]);

        Assert.True(response.IsSuccess);
        Assert.Equal("adapter:hello", response.Content);
        Assert.Equal("hello", adapter.LastRequestContext?.Messages[0].Content);
        Assert.Same(response, completed);
    }

    [Fact]
    public async Task SendMessageWithHistoryAsync_RaisesErrorOccurred_WhenExecutionFails()
    {
        var adapter = new FailingAdapter();
        var runtimeWorkflow = new ProviderRuntimeApplicationService(new ProviderAdapterApplicationService([adapter]));
        var service = new ChatService(_factory, runtimeWorkflow, new ChatOrchestrator());
        typeof(ChatService)
            .GetField("_currentProvider", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(service, new FakeProvider());

        string? error = null;
        service.ErrorOccurred += (_, message) => error = message;

        var response = await service.SendMessageWithHistoryAsync(
        [
            new ChatMessage
            {
                Role = "user",
                Content = "hello"
            }
        ]);

        Assert.False(response.IsSuccess);
        Assert.Equal("boom", response.ErrorMessage);
        Assert.Equal("boom", error);
    }

    [Fact]
    public async Task SendMessageWithHistoryAsync_ReturnsGenericError_OnUnexpectedException()
    {
        var adapter = new ThrowingAdapter();
        var runtimeWorkflow = new ProviderRuntimeApplicationService(new ProviderAdapterApplicationService([adapter]));
        var service = new ChatService(_factory, runtimeWorkflow, new ChatOrchestrator());
        typeof(ChatService)
            .GetField("_currentProvider", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(service, new FakeProvider());

        string? error = null;
        service.ErrorOccurred += (_, message) => error = message;

        var response = await service.SendMessageWithHistoryAsync(
        [
            new ChatMessage
            {
                Role = "user",
                Content = "hello"
            }
        ]);

        Assert.False(response.IsSuccess);
        Assert.Equal("An unexpected error occurred. Please try again.", response.ErrorMessage);
        Assert.Equal("An unexpected error occurred. Please try again.", error);
    }

    [Fact]
    public async Task SendMessageWithHistoryAsync_PropagatesCancellation()
    {
        var adapter = new CancelingAdapter();
        var runtimeWorkflow = new ProviderRuntimeApplicationService(new ProviderAdapterApplicationService([adapter]));
        var service = new ChatService(_factory, runtimeWorkflow, new ChatOrchestrator());
        typeof(ChatService)
            .GetField("_currentProvider", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(service, new FakeProvider());

        await Assert.ThrowsAsync<OperationCanceledException>(() => service.SendMessageWithHistoryAsync(
        [
            new ChatMessage
            {
                Role = "user",
                Content = "hello"
            }
        ]));
    }

    [Fact]
    public async Task StreamMessageWithHistoryAsync_PropagatesCancellation()
    {
        var orchestrator = new ChatOrchestrator();
        var service = new ChatService(_factory, new ProviderRuntimeApplicationService(new ProviderAdapterApplicationService([new RecordingAdapter()])), orchestrator);
        typeof(ChatService)
            .GetField("_currentProvider", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(service, new CancelingStreamingProvider());

        await Assert.ThrowsAsync<OperationCanceledException>(() => service.StreamMessageWithHistoryAsync(
        [
            new ChatMessage
            {
                Role = "user",
                Content = "hello"
            }
        ]));
    }

    [Fact]
    public async Task StreamMessageAsync_ForwardsStreamingEventsFromOrchestrator()
    {
        var orchestrator = new ChatOrchestrator();
        orchestrator.SetProvider(new StreamingFakeProvider());
        var service = new ChatService(_factory, new ProviderRuntimeApplicationService(new ProviderAdapterApplicationService([new RecordingAdapter()])), orchestrator);

        var chunks = new List<string>();
        AiResponse? completed = null;
        service.ResponseChunkReceived += (_, chunk) => chunks.Add(chunk);
        service.ResponseCompleted += (_, response) => completed = response;

        await service.StreamMessageAsync("hello");

        Assert.Equal(["echo:", "hello"], chunks);
        Assert.NotNull(completed);
        Assert.True(completed!.IsSuccess);
        Assert.Equal("echo:hello", completed.Content);
    }

    [Fact]
    public async Task StreamMessageWithHistoryAsync_ForwardsStreamingHistoryThroughOrchestrator()
    {
        var orchestrator = new ChatOrchestrator();
        orchestrator.SetProvider(new StreamingFakeProvider());
        var service = new ChatService(_factory, new ProviderRuntimeApplicationService(new ProviderAdapterApplicationService([new RecordingAdapter()])), orchestrator);
        typeof(ChatService)
            .GetField("_currentProvider", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(service, new StreamingFakeProvider());

        var chunks = new List<string>();
        service.ResponseChunkReceived += (_, chunk) => chunks.Add(chunk);

        var response = await service.StreamMessageWithHistoryAsync(
        [
            new ChatMessage
            {
                Role = "user",
                Content = "hello"
            }
        ]);

        Assert.True(response.IsSuccess);
        Assert.Equal("echo:hello", response.Content);
        Assert.Equal(["echo:", "hello"], chunks);
    }

    private sealed class RecordingAdapter : IProviderAdapter
    {
        public string ProviderType => "Fake";
        public ProviderRequestContext? LastRequestContext { get; private set; }

        public bool CanHandle(string providerType)
            => string.Equals(providerType, ProviderType, StringComparison.OrdinalIgnoreCase);

        public IAiProvider? BuildProvider(ProviderRuntimeRequest request) => null;

        public Task<ProviderExecutionResult> ExecuteAsync(IAiProvider provider, ProviderRequestContext requestContext)
        {
            LastRequestContext = requestContext;
            return Task.FromResult(ProviderExecutionResult.Succeeded(
                WorkflowIntent.AssistantText("adapter:hello"),
                rawContent: "adapter:hello"));
        }

        public Task<ProviderSmokeTestResult> RunSmokeTestAsync(IAiProvider provider, CancellationToken cancellationToken)
            => Task.FromResult(new ProviderSmokeTestResult(true));

        public Task<ProviderValidationOutcome> ValidateAsync(IAiProvider provider, CancellationToken cancellationToken)
            => Task.FromResult(ProviderValidationOutcome.Valid());
    }

    private sealed class FailingAdapter : IProviderAdapter
    {
        public string ProviderType => "Fake";

        public bool CanHandle(string providerType)
            => string.Equals(providerType, ProviderType, StringComparison.OrdinalIgnoreCase);

        public IAiProvider? BuildProvider(ProviderRuntimeRequest request) => null;

        public Task<ProviderExecutionResult> ExecuteAsync(IAiProvider provider, ProviderRequestContext requestContext)
            => Task.FromResult(ProviderExecutionResult.Failed("boom"));

        public Task<ProviderSmokeTestResult> RunSmokeTestAsync(IAiProvider provider, CancellationToken cancellationToken)
            => Task.FromResult(new ProviderSmokeTestResult(false, "boom"));

        public Task<ProviderValidationOutcome> ValidateAsync(IAiProvider provider, CancellationToken cancellationToken)
            => Task.FromResult(ProviderValidationOutcome.Invalid("boom"));
    }

    private sealed class ThrowingAdapter : IProviderAdapter
    {
        public string ProviderType => "Fake";

        public bool CanHandle(string providerType)
            => string.Equals(providerType, ProviderType, StringComparison.OrdinalIgnoreCase);

        public IAiProvider? BuildProvider(ProviderRuntimeRequest request) => null;

        public Task<ProviderExecutionResult> ExecuteAsync(IAiProvider provider, ProviderRequestContext requestContext)
            => throw new InvalidOperationException("sensitive internal details");

        public Task<ProviderSmokeTestResult> RunSmokeTestAsync(IAiProvider provider, CancellationToken cancellationToken)
            => Task.FromResult(new ProviderSmokeTestResult(false, "boom"));

        public Task<ProviderValidationOutcome> ValidateAsync(IAiProvider provider, CancellationToken cancellationToken)
            => Task.FromResult(ProviderValidationOutcome.Invalid("boom"));
    }

    private sealed class CancelingAdapter : IProviderAdapter
    {
        public string ProviderType => "Fake";

        public bool CanHandle(string providerType)
            => string.Equals(providerType, ProviderType, StringComparison.OrdinalIgnoreCase);

        public IAiProvider? BuildProvider(ProviderRuntimeRequest request) => null;

        public Task<ProviderExecutionResult> ExecuteAsync(IAiProvider provider, ProviderRequestContext requestContext)
            => throw new OperationCanceledException();

        public Task<ProviderSmokeTestResult> RunSmokeTestAsync(IAiProvider provider, CancellationToken cancellationToken)
            => Task.FromResult(new ProviderSmokeTestResult(false, "canceled"));

        public Task<ProviderValidationOutcome> ValidateAsync(IAiProvider provider, CancellationToken cancellationToken)
            => Task.FromResult(ProviderValidationOutcome.Invalid("canceled"));
    }

    private sealed class FakeProvider : IAiProvider
    {
        public string ProviderType => "Fake";
        public string DisplayName => "Fake";
        public ProviderCapabilities Capabilities => ProviderCapabilities.TextChat;
        public ToolCallMode ToolCallMode => ToolCallMode.TextBased;
        public ToolOutputFormat ToolOutputFormat => ToolOutputFormat.AireText;

        public bool Has(ProviderCapabilities cap) => (Capabilities & cap) == cap;
        public void Initialize(ProviderConfig config) { }
        public Task<AiResponse> SendChatAsync(IEnumerable<ChatMessage> messages, CancellationToken cancellationToken = default)
            => Task.FromResult(new AiResponse());
        public void PrepareForCapabilityTesting() { }
        public void SetToolsEnabled(bool enabled) { }
        public void SetEnabledToolCategories(IEnumerable<string>? categories) { }
        public async IAsyncEnumerable<string> StreamChatAsync(IEnumerable<ChatMessage> messages, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }
        public Task<ProviderValidationResult> ValidateConfigurationAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(ProviderValidationResult.Ok());
        public Task<TokenUsage?> GetTokenUsageAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<TokenUsage?>(null);
    }

    private sealed class StreamingFakeProvider : IAiProvider
    {
        public string ProviderType => "Fake";
        public string DisplayName => "Fake";
        public ProviderCapabilities Capabilities => ProviderCapabilities.TextChat | ProviderCapabilities.Streaming;
        public ToolCallMode ToolCallMode => ToolCallMode.TextBased;
        public ToolOutputFormat ToolOutputFormat => ToolOutputFormat.AireText;

        public bool Has(ProviderCapabilities cap) => (Capabilities & cap) == cap;
        public void Initialize(ProviderConfig config) { }
        public Task<AiResponse> SendChatAsync(IEnumerable<ChatMessage> messages, CancellationToken cancellationToken = default)
            => Task.FromResult(new AiResponse { IsSuccess = true, Content = "echo:hello" });
        public void PrepareForCapabilityTesting() { }
        public void SetToolsEnabled(bool enabled) { }
        public void SetEnabledToolCategories(IEnumerable<string>? categories) { }
        public async IAsyncEnumerable<string> StreamChatAsync(IEnumerable<ChatMessage> messages, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield return "echo:";
            yield return "hello";
        }
        public Task<ProviderValidationResult> ValidateConfigurationAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(ProviderValidationResult.Ok());
        public Task<TokenUsage?> GetTokenUsageAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<TokenUsage?>(null);
    }

    private sealed class CancelingStreamingProvider : IAiProvider
    {
        public string ProviderType => "Fake";
        public string DisplayName => "Fake";
        public ProviderCapabilities Capabilities => ProviderCapabilities.TextChat | ProviderCapabilities.Streaming;
        public ToolCallMode ToolCallMode => ToolCallMode.TextBased;
        public ToolOutputFormat ToolOutputFormat => ToolOutputFormat.AireText;

        public bool Has(ProviderCapabilities cap) => (Capabilities & cap) == cap;
        public void Initialize(ProviderConfig config) { }
        public Task<AiResponse> SendChatAsync(IEnumerable<ChatMessage> messages, CancellationToken cancellationToken = default)
            => Task.FromResult(new AiResponse());
        public void PrepareForCapabilityTesting() { }
        public void SetToolsEnabled(bool enabled) { }
        public void SetEnabledToolCategories(IEnumerable<string>? categories) { }
        public async IAsyncEnumerable<string> StreamChatAsync(IEnumerable<ChatMessage> messages, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield return "partial";
            throw new OperationCanceledException();
        }
        public Task<ProviderValidationResult> ValidateConfigurationAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(ProviderValidationResult.Ok());
        public Task<TokenUsage?> GetTokenUsageAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<TokenUsage?>(null);
    }
}
