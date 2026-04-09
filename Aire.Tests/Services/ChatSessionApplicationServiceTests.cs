using System.Collections.Generic;
using System.Threading.Tasks;
using Aire.AppLayer.Chat;
using Aire.AppLayer.Abstractions;
using Aire.Data;
using Xunit;

namespace Aire.Tests.Services;

public sealed class ChatSessionApplicationServiceTests
{
    [Fact]
    public async Task SaveAndLoadSelectedProviderId_RoundTripsThroughSettingsRepository()
    {
        var conversations = new FakeConversationRepository();
        var settings = new FakeSettingsRepository();
        var service = new ChatSessionApplicationService(conversations, settings);

        await service.SaveSelectedProviderAsync(42);

        Assert.Equal("42", settings.GetValue("SelectedProviderId"));
        Assert.Equal(42, await service.GetSelectedProviderIdAsync());
    }

    [Fact]
    public async Task PersistUserMessageAsync_SavesMessage_AndUpdatesTitleWhenProvided()
    {
        var conversations = new FakeConversationRepository();
        var settings = new FakeSettingsRepository();
        var service = new ChatSessionApplicationService(conversations, settings);

        await service.PersistUserMessageAsync(
            conversationId: 8,
            content: "Hello there",
            imagePath: "image.png",
            attachments: null,
            suggestedConversationTitle: "Greeting");

        Assert.Single(conversations.SavedMessages);
        Assert.Equal((8, "user", "Hello there", "image.png"), conversations.SavedMessages[0]);
        Assert.Equal((8, "Greeting"), conversations.UpdatedTitles[0]);
    }

    [Fact]
    public async Task PersistUserMessageAsync_SkipsTitleUpdate_WhenTitleMissing()
    {
        var conversations = new FakeConversationRepository();
        var settings = new FakeSettingsRepository();
        var service = new ChatSessionApplicationService(conversations, settings);

        await service.PersistUserMessageAsync(
            conversationId: 8,
            content: "Hello there",
            imagePath: null,
            attachments: null,
            suggestedConversationTitle: "  ");

        Assert.Single(conversations.SavedMessages);
        Assert.Empty(conversations.UpdatedTitles);
    }

    [Fact]
    public async Task UpdateConversationProviderAsync_ForwardsToConversationRepository()
    {
        var conversations = new FakeConversationRepository();
        var settings = new FakeSettingsRepository();
        var service = new ChatSessionApplicationService(conversations, settings);

        await service.UpdateConversationProviderAsync(12, 99);

        Assert.Equal((12, 99), conversations.UpdatedProviders[0]);
    }

    private sealed class FakeConversationRepository : IConversationRepository
    {
        public List<(int ConversationId, string Role, string Content, string? ImagePath)> SavedMessages { get; } = [];
        public List<(int ConversationId, string Title)> UpdatedTitles { get; } = [];
        public List<(int ConversationId, int ProviderId)> UpdatedProviders { get; } = [];

        public Task<int> CreateConversationAsync(int providerId, string title) => throw new System.NotSupportedException();
        public Task<Conversation?> GetLatestConversationAsync(int providerId) => throw new System.NotSupportedException();
        public Task<Conversation?> GetConversationAsync(int conversationId) => throw new System.NotSupportedException();
        public Task<List<ConversationSummary>> ListConversationsAsync(string? search = null) => throw new System.NotSupportedException();

        public Task UpdateConversationTitleAsync(int conversationId, string title)
        {
            UpdatedTitles.Add((conversationId, title));
            return Task.CompletedTask;
        }

        public Task UpdateConversationProviderAsync(int conversationId, int providerId)
        {
            UpdatedProviders.Add((conversationId, providerId));
            return Task.CompletedTask;
        }

        public Task UpdateConversationAssistantModeAsync(int conversationId, string assistantModeKey) => Task.CompletedTask;

        public Task SaveMessageAsync(
            int conversationId,
            string role,
            string content,
            string? imagePath = null,
            IEnumerable<MessageAttachment>? attachments = null)
        {
            SavedMessages.Add((conversationId, role, content, imagePath));
            return Task.CompletedTask;
        }

        public Task<List<Message>> GetMessagesAsync(int conversationId) => throw new System.NotSupportedException();
        public Task DeleteMessagesByConversationIdAsync(int conversationId) => throw new System.NotSupportedException();
        public Task DeleteConversationAsync(int conversationId) => throw new System.NotSupportedException();
        public Task DeleteAllConversationsAsync() => throw new System.NotSupportedException();
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

        public Task LogFileAccessAsync(string operation, string path, bool allowed) => Task.CompletedTask;

        public string? GetValue(string key) => _settings.TryGetValue(key, out var value) ? value : null;
    }
}
