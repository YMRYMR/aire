using System.Collections.Generic;
using System.Threading.Tasks;
using Aire.AppLayer.Abstractions;
using Aire.AppLayer.Chat;
using Aire.AppLayer.Providers;
using Aire.Data;
using Aire.Providers;
using Aire.Services;
using Aire.Services.Workflows;
using Xunit;

namespace Aire.Tests.Services;

public sealed class ProviderActivationApplicationServiceTests
{
    [Fact]
    public async Task ActivateProviderAsync_LoadsLatestConversationAndPersistsSelection()
    {
        var providerRepository = new FakeProviderRepository();
        var providerOne = providerRepository.AddProvider(new Provider
        {
            Name = "Provider One",
            Type = "OpenAI",
            ApiKey = "sk-one",
            Model = "gpt-4.1-mini",
            IsEnabled = true,
            Color = "#123456"
        });
        var providerTwo = providerRepository.AddProvider(new Provider
        {
            Name = "Provider Two",
            Type = "OpenAI",
            ApiKey = "sk-two",
            Model = "gpt-4o-mini",
            IsEnabled = true,
            Color = "#654321"
        });

        var conversations = new FakeConversationRepository();
        var latestConversation = conversations.AddConversation(providerTwo.Id, "Latest Two");
        var settings = new FakeSettingsRepository();
        var chatSession = new ChatSessionApplicationService(conversations, settings);

        using var chatService = new ChatService(new ProviderFactory(providerRepository));
        var service = new ProviderActivationApplicationService(chatService, new ProviderFactory(providerRepository), chatSession);

        var result = await service.ActivateProviderAsync(providerTwo, providerOne.Id, null, showSwitchedMessage: true);

        Assert.NotNull(result.ProviderInstance);
        Assert.Equal(ProviderActivationWorkflowService.ConversationActionKind.LoadExistingConversation, result.ActivationPlan.ConversationAction);
        Assert.Equal(latestConversation.Id, result.ActivationPlan.ConversationIdToLoad);
        Assert.False(result.ActivationPlan.ShouldAnnounceSwitch);
        Assert.Contains(providerTwo.Name, result.SwitchedProviderMessage);
        Assert.Equal(providerTwo.Id.ToString(), settings.GetValue("SelectedProviderId"));
    }

    [Fact]
    public async Task ActivateProviderAsync_KeepCurrentConversation_UpdatesConversationProvider()
    {
        var providerRepository = new FakeProviderRepository();
        var providerOne = providerRepository.AddProvider(new Provider
        {
            Name = "Provider One",
            Type = "OpenAI",
            ApiKey = "sk-one",
            Model = "gpt-4.1-mini",
            IsEnabled = true,
            Color = "#123456"
        });
        var providerTwo = providerRepository.AddProvider(new Provider
        {
            Name = "Provider Two",
            Type = "OpenAI",
            ApiKey = "sk-two",
            Model = "gpt-4o-mini",
            IsEnabled = true,
            Color = "#654321"
        });

        var conversations = new FakeConversationRepository();
        var currentConversation = conversations.AddConversation(providerOne.Id, "Current");
        var settings = new FakeSettingsRepository();
        var chatSession = new ChatSessionApplicationService(conversations, settings);

        using var chatService = new ChatService(new ProviderFactory(providerRepository));
        var service = new ProviderActivationApplicationService(chatService, new ProviderFactory(providerRepository), chatSession);

        var result = await service.ActivateProviderAsync(providerTwo, providerOne.Id, currentConversation.Id, showSwitchedMessage: true);

        Assert.NotNull(result.ProviderInstance);
        Assert.Equal(ProviderActivationWorkflowService.ConversationActionKind.KeepCurrentConversation, result.ActivationPlan.ConversationAction);
        Assert.Equal(currentConversation.Id, result.ActivationPlan.ConversationIdToLoad);
        Assert.True(result.ActivationPlan.ProviderChanged);
        Assert.True(result.ActivationPlan.ShouldAnnounceSwitch);
        Assert.Equal(providerTwo.Id.ToString(), settings.GetValue("SelectedProviderId"));
        Assert.Equal(providerTwo.Id, conversations.GetConversation(currentConversation.Id)!.ProviderId);
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
            if (index >= 0)
                _providers[index] = Clone(provider);
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

        private static Provider Clone(Provider provider)
            => new()
            {
                Id = provider.Id,
                Name = provider.Name,
                Type = provider.Type,
                ApiKey = provider.ApiKey,
                BaseUrl = provider.BaseUrl,
                Model = provider.Model,
                IsEnabled = provider.IsEnabled,
                Color = provider.Color,
                SortOrder = provider.SortOrder,
                TimeoutMinutes = provider.TimeoutMinutes
            };
    }

    private sealed class FakeConversationRepository : IConversationRepository
    {
        private readonly List<Aire.Data.Conversation> _conversations = [];
        private int _nextId = 1;

        public Aire.Data.Conversation AddConversation(int providerId, string title)
        {
            var conversation = new Aire.Data.Conversation
            {
                Id = _nextId++,
                ProviderId = providerId,
                Title = title
            };
            _conversations.Add(conversation);
            return conversation;
        }

        public Aire.Data.Conversation? GetConversation(int conversationId)
            => _conversations.Find(conversation => conversation.Id == conversationId);

        public Task<int> CreateConversationAsync(int providerId, string title)
            => Task.FromResult(AddConversation(providerId, title).Id);

        public Task<Aire.Data.Conversation?> GetLatestConversationAsync(int providerId)
            => Task.FromResult<Aire.Data.Conversation?>(_conversations.FindLast(conversation => conversation.ProviderId == providerId));

        public Task<Aire.Data.Conversation?> GetConversationAsync(int conversationId)
            => Task.FromResult(GetConversation(conversationId));

        public Task<List<ConversationSummary>> ListConversationsAsync(string? search = null)
            => Task.FromResult(new List<ConversationSummary>());

        public Task UpdateConversationTitleAsync(int conversationId, string title)
        {
            var conversation = GetConversation(conversationId);
            if (conversation != null)
                conversation.Title = title;
            return Task.CompletedTask;
        }

        public Task UpdateConversationProviderAsync(int conversationId, int providerId)
        {
            var conversation = GetConversation(conversationId);
            if (conversation != null)
                conversation.ProviderId = providerId;
            return Task.CompletedTask;
        }

        public Task UpdateConversationAssistantModeAsync(int conversationId, string assistantModeKey) => Task.CompletedTask;
        public Task SaveMessageAsync(int conversationId, string role, string content, string? imagePath = null, IEnumerable<MessageAttachment>? attachments = null) => Task.CompletedTask;
        public Task<List<Message>> GetMessagesAsync(int conversationId) => Task.FromResult(new List<Message>());
        public Task DeleteMessagesByConversationIdAsync(int conversationId) => Task.CompletedTask;
        public Task DeleteConversationAsync(int conversationId) => Task.CompletedTask;
        public Task DeleteAllConversationsAsync() => Task.CompletedTask;
    }

    private sealed class FakeSettingsRepository : ISettingsRepository
    {
        private readonly Dictionary<string, string> _settings = [];

        public Task<string?> GetSettingAsync(string key)
            => Task.FromResult(_settings.TryGetValue(key, out var value) ? value : null);

        public Task SetSettingAsync(string key, string value)
        {
            _settings[key] = value;
            return Task.CompletedTask;
        }

        public Task LogFileAccessAsync(string operation, string path, bool allowed)
            => Task.CompletedTask;

        public string? GetValue(string key) => _settings.TryGetValue(key, out var value) ? value : null;
    }
}
