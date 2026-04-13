using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Aire.AppLayer.Abstractions;
using Aire.AppLayer.Chat;
using Aire.AppLayer.Providers;
using Aire.Data;
using Aire.Providers;
using Aire.Services;
using Xunit;

namespace Aire.Tests.Services;

public sealed class SwitchModelApplicationServiceTests
{
    [Fact]
    public async Task ExecuteAsync_EmptyModelName_ReturnsFailure()
    {
        var svc = CreateService();
        var toolCall = MakeToolCall("", "need a bigger model");

        var result = await svc.ExecuteAsync("", toolCall, [], _ => false, null);

        Assert.False(result.Succeeded);
        Assert.Null(result.RequestedModel);
        Assert.Contains("model_name parameter is required", result.ResultHistoryMessage.Content);
    }

    [Fact]
    public async Task ExecuteAsync_NoMatchingProvider_ReturnsFailure()
    {
        var svc = CreateService();
        var toolCall = MakeToolCall("nonexistent-model", "testing");

        var result = await svc.ExecuteAsync("", toolCall, [], _ => false, null);

        Assert.False(result.Succeeded);
        Assert.Equal("nonexistent-model", result.RequestedModel);
        Assert.Contains("no available provider", result.ResultHistoryMessage.Content);
    }

    [Fact]
    public async Task ExecuteAsync_DisabledProvider_SkipsIt()
    {
        var repo = new FakeProviderRepository();
        var provider = repo.AddProvider(new Provider
        {
            Name = "Disabled", Type = "OpenAI", ApiKey = "sk-test",
            Model = "gpt-4o-mini", IsEnabled = false, Color = "#000"
        });
        var svc = CreateService(repo);
        var toolCall = MakeToolCall("gpt-4o-mini", "");

        var result = await svc.ExecuteAsync("", toolCall, [provider], _ => false, null);

        Assert.False(result.Succeeded);
        Assert.Contains("no available provider", result.ResultHistoryMessage.Content);
    }

    [Fact]
    public async Task ExecuteAsync_CooldownProvider_SkipsIt()
    {
        var repo = new FakeProviderRepository();
        var provider = repo.AddProvider(new Provider
        {
            Name = "OnCooldown", Type = "OpenAI", ApiKey = "sk-test",
            Model = "gpt-4o-mini", IsEnabled = true, Color = "#000"
        });
        var svc = CreateService(repo);
        var toolCall = MakeToolCall("gpt-4o-mini", "");

        var result = await svc.ExecuteAsync("", toolCall, [provider], id => true, null);

        Assert.False(result.Succeeded);
        Assert.Contains("no available provider", result.ResultHistoryMessage.Content);
    }

    [Fact]
    public async Task ExecuteAsync_ValidProvider_Succeeds()
    {
        var repo = new FakeProviderRepository();
        var provider = repo.AddProvider(new Provider
        {
            Name = "Test Provider", Type = "OpenAI", ApiKey = "sk-test",
            Model = "gpt-4o-mini", IsEnabled = true, Color = "#007ACC"
        });
        var settings = new FakeSettingsRepository();
        var conversations = new FakeConversationRepository();
        var chatSession = new ChatSessionApplicationService(conversations, settings);
        using var chatService = new ChatService(new ProviderFactory(repo));
        var svc = new SwitchModelApplicationService(new ProviderFactory(repo), chatService, chatSession);

        var toolCall = MakeToolCall("gpt-4o-mini", "faster responses");
        var result = await svc.ExecuteAsync("switching now", toolCall, [provider], _ => false, null);

        Assert.True(result.Succeeded);
        Assert.Equal("gpt-4o-mini", result.RequestedModel);
        Assert.Equal(provider, result.TargetProvider);
        Assert.NotNull(result.ProviderInstance);
        Assert.Contains("SUCCESS", result.ResultHistoryMessage.Content);
        Assert.Contains("Test Provider", result.UserFacingMessage);
        Assert.Contains("faster responses", result.UserFacingMessage);
        Assert.Equal(provider.Id.ToString(), settings.GetValue("SelectedProviderId"));
    }

    [Fact]
    public async Task ExecuteAsync_CaseInsensitiveModelMatch()
    {
        var repo = new FakeProviderRepository();
        var provider = repo.AddProvider(new Provider
        {
            Name = "Test", Type = "OpenAI", ApiKey = "sk-test",
            Model = "GPT-4O-MINI", IsEnabled = true, Color = "#000"
        });
        var svc = CreateService(repo);
        var toolCall = MakeToolCall("gpt-4o-mini", "");

        var result = await svc.ExecuteAsync("", toolCall, [provider], _ => false, null);

        Assert.True(result.Succeeded);
        Assert.Equal(provider, result.TargetProvider);
    }

    [Fact]
    public async Task ExecuteAsync_WithConversationId_UpdatesConversationProvider()
    {
        var repo = new FakeProviderRepository();
        var oldProvider = repo.AddProvider(new Provider
        {
            Name = "Old", Type = "OpenAI", ApiKey = "sk-old",
            Model = "gpt-4.1-mini", IsEnabled = true, Color = "#111"
        });
        var newProvider = repo.AddProvider(new Provider
        {
            Name = "New", Type = "OpenAI", ApiKey = "sk-new",
            Model = "gpt-4o-mini", IsEnabled = true, Color = "#222"
        });
        var conversations = new FakeConversationRepository();
        var conversation = conversations.AddConversation(oldProvider.Id, "Test");
        var settings = new FakeSettingsRepository();
        var chatSession = new ChatSessionApplicationService(conversations, settings);
        using var chatService = new ChatService(new ProviderFactory(repo));
        var svc = new SwitchModelApplicationService(new ProviderFactory(repo), chatService, chatSession);

        var toolCall = MakeToolCall("gpt-4o-mini", "");
        var result = await svc.ExecuteAsync("", toolCall, [oldProvider, newProvider], _ => false, conversation.Id);

        Assert.True(result.Succeeded);
        Assert.Equal(newProvider.Id, conversations.GetConversation(conversation.Id)!.ProviderId);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutReason_NoReasonInUserMessage()
    {
        var repo = new FakeProviderRepository();
        var provider = repo.AddProvider(new Provider
        {
            Name = "Test", Type = "OpenAI", ApiKey = "sk-test",
            Model = "gpt-4o-mini", IsEnabled = true, Color = "#000"
        });
        var svc = CreateService(repo);
        var toolCall = MakeToolCall("gpt-4o-mini", "");

        var result = await svc.ExecuteAsync("", toolCall, [provider], _ => false, null);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.UserFacingMessage);
        Assert.DoesNotContain("—", result.UserFacingMessage!);
    }

    [Fact]
    public async Task ExecuteAsync_AssistantHistoryMessage_ContainsToolCallJson()
    {
        var repo = new FakeProviderRepository();
        var provider = repo.AddProvider(new Provider
        {
            Name = "Test", Type = "OpenAI", ApiKey = "sk-test",
            Model = "gpt-4o-mini", IsEnabled = true, Color = "#000"
        });
        var svc = CreateService(repo);
        var toolCall = MakeToolCall("gpt-4o-mini", "test reason");

        var result = await svc.ExecuteAsync("let me switch", toolCall, [provider], _ => false, null);

        Assert.Equal("assistant", result.AssistantHistoryMessage.Role);
        Assert.Contains("let me switch", result.AssistantHistoryMessage.Content);
    }

    [Fact]
    public async Task ExecuteAsync_ResultHistoryMessage_HasUserRole()
    {
        var svc = CreateService();
        var toolCall = MakeToolCall("", "");

        var result = await svc.ExecuteAsync("", toolCall, [], _ => false, null);

        Assert.Equal("user", result.ResultHistoryMessage.Role);
    }

    [Fact]
    public async Task ExecuteAsync_ParsedOverload_DelegatesCorrectly()
    {
        var repo = new FakeProviderRepository();
        var provider = repo.AddProvider(new Provider
        {
            Name = "Test", Type = "OpenAI", ApiKey = "sk-test",
            Model = "gpt-4o-mini", IsEnabled = true, Color = "#000"
        });
        var svc = CreateService(repo);

        var parsed = new ParsedAiResponse
        {
            TextContent = "switching model",
            ToolCall = MakeToolCall("gpt-4o-mini", "better fit")
        };

        var result = await svc.ExecuteAsync(parsed, [provider], _ => false, null);

        Assert.True(result.Succeeded);
        Assert.Equal("gpt-4o-mini", result.RequestedModel);
    }

    private static SwitchModelApplicationService CreateService(FakeProviderRepository? repo = null)
    {
        repo ??= new FakeProviderRepository();
        var settings = new FakeSettingsRepository();
        var conversations = new FakeConversationRepository();
        var chatSession = new ChatSessionApplicationService(conversations, settings);
        var chatService = new ChatService(new ProviderFactory(repo));
        return new SwitchModelApplicationService(new ProviderFactory(repo), chatService, chatSession);
    }

    private static ToolCallRequest MakeToolCall(string modelName, string reason)
    {
        var json = $"{{\"model_name\":\"{modelName}\",\"reason\":\"{reason}\"}}";
        return new ToolCallRequest
        {
            Tool = "switch_model",
            Parameters = JsonDocument.Parse(json).RootElement,
            RawJson = json
        };
    }

    private sealed class FakeProviderRepository : IProviderRepository
    {
        private readonly List<Provider> _providers = [];
        private int _nextId = 1;

        public Provider AddProvider(Provider provider)
        {
            provider.Id = _nextId++;
            _providers.Add(Clone(provider));
            return Clone(provider);
        }

        public Task<List<Provider>> GetProvidersAsync()
            => Task.FromResult(new List<Provider>(_providers));

        public Task UpdateProviderAsync(Provider provider)
        {
            var index = _providers.FindIndex(p => p.Id == provider.Id);
            if (index >= 0) _providers[index] = Clone(provider);
            return Task.CompletedTask;
        }

        public Task SaveProviderOrderAsync(IEnumerable<Provider> orderedProviders) => Task.CompletedTask;

        public Task<int> InsertProviderAsync(Provider provider)
        {
            provider.Id = _nextId++;
            _providers.Add(Clone(provider));
            return Task.FromResult(provider.Id);
        }

        public Task DeleteProviderAsync(int id)
        {
            _providers.RemoveAll(p => p.Id == id);
            return Task.CompletedTask;
        }

        private static Provider Clone(Provider p) => new()
        {
            Id = p.Id, Name = p.Name, Type = p.Type, ApiKey = p.ApiKey,
            BaseUrl = p.BaseUrl, Model = p.Model, IsEnabled = p.IsEnabled,
            Color = p.Color, SortOrder = p.SortOrder, TimeoutMinutes = p.TimeoutMinutes
        };
    }

    private sealed class FakeConversationRepository : IConversationRepository
    {
        private readonly List<Aire.Data.Conversation> _conversations = [];
        private int _nextId = 1;

        public Aire.Data.Conversation AddConversation(int providerId, string title)
        {
            var c = new Aire.Data.Conversation { Id = _nextId++, ProviderId = providerId, Title = title };
            _conversations.Add(c);
            return c;
        }

        public Aire.Data.Conversation? GetConversation(int id)
            => _conversations.Find(c => c.Id == id);

        public Task<int> CreateConversationAsync(int providerId, string title)
            => Task.FromResult(AddConversation(providerId, title).Id);

        public Task<Aire.Data.Conversation?> GetLatestConversationAsync(int providerId)
            => Task.FromResult<Aire.Data.Conversation?>(_conversations.FindLast(c => c.ProviderId == providerId));

        public Task<Aire.Data.Conversation?> GetConversationAsync(int id)
            => Task.FromResult(GetConversation(id));

        public Task<List<ConversationSummary>> ListConversationsAsync(string? search = null)
            => Task.FromResult(new List<ConversationSummary>());

        public Task UpdateConversationTitleAsync(int id, string title) => Task.CompletedTask;

        public Task UpdateConversationProviderAsync(int id, int providerId)
        {
            var c = GetConversation(id);
            if (c != null) c.ProviderId = providerId;
            return Task.CompletedTask;
        }

        public Task UpdateConversationAssistantModeAsync(int id, string key) => Task.CompletedTask;
        public Task SaveMessageAsync(int id, string role, string content, string? imagePath = null, IEnumerable<MessageAttachment>? attachments = null, int? tokens = null) => Task.CompletedTask;
        public Task<List<Message>> GetMessagesAsync(int id) => Task.FromResult(new List<Message>());
        public Task DeleteMessagesByConversationIdAsync(int id) => Task.CompletedTask;
        public Task DeleteConversationAsync(int id) => Task.CompletedTask;
        public Task DeleteAllConversationsAsync() => Task.CompletedTask;
    }

    private sealed class FakeSettingsRepository : ISettingsRepository
    {
        private readonly Dictionary<string, string> _settings = [];

        public Task<string?> GetSettingAsync(string key)
            => Task.FromResult(_settings.TryGetValue(key, out var v) ? v : null);

        public Task SetSettingAsync(string key, string value)
        {
            _settings[key] = value;
            return Task.CompletedTask;
        }

        public Task LogFileAccessAsync(string operation, string path, bool allowed) => Task.CompletedTask;
        public string? GetValue(string key) => _settings.TryGetValue(key, out var v) ? v : null;
    }
}
