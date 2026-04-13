using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Aire.AppLayer.Abstractions;
using Aire.AppLayer.Chat;
using Aire.Data;
using Xunit;

namespace Aire.Tests.Services;

public sealed class ConversationExportServiceTests
{
    [Fact]
    public async Task ExportMarkdownAsync_ThrowsForMissingConversation()
    {
        var svc = CreateService();
        await Assert.ThrowsAsync<ArgumentException>(
            () => svc.ExportMarkdownAsync(999));
    }

    [Fact]
    public async Task ExportMarkdownAsync_IncludesTitle()
    {
        var conversations = new FakeConversationRepo();
        conversations.AddConversation(1, "Test Chat");
        var svc = CreateService(conversations);

        var md = await svc.ExportMarkdownAsync(1);

        Assert.Contains("# Test Chat", md);
    }

    [Fact]
    public async Task ExportMarkdownAsync_IncludesProviderInfo()
    {
        var conversations = new FakeConversationRepo();
        conversations.AddConversation(1, "Chat");
        var providers = new FakeProviderRepo();
        providers.Add(new Provider { Id = 1, Name = "GPT", Model = "gpt-4o" });
        var svc = CreateService(conversations, providers);

        var md = await svc.ExportMarkdownAsync(1);

        Assert.Contains("GPT", md);
        Assert.Contains("gpt-4o", md);
    }

    [Fact]
    public async Task ExportMarkdownAsync_IncludesUserMessage()
    {
        var conversations = new FakeConversationRepo();
        conversations.AddConversation(1, "Chat");
        conversations.AddMessage(1, "user", "Hello AI");
        var svc = CreateService(conversations);

        var md = await svc.ExportMarkdownAsync(1);

        Assert.Contains("### User", md);
        Assert.Contains("Hello AI", md);
    }

    [Fact]
    public async Task ExportMarkdownAsync_IncludesAssistantMessage()
    {
        var conversations = new FakeConversationRepo();
        conversations.AddConversation(1, "Chat");
        conversations.AddMessage(1, "assistant", "Hello! How can I help?");
        var svc = CreateService(conversations);

        var md = await svc.ExportMarkdownAsync(1);

        Assert.Contains("### AI", md);
        Assert.Contains("Hello! How can I help?", md);
    }

    [Fact]
    public async Task ExportMarkdownAsync_IncludesSystemMessage()
    {
        var conversations = new FakeConversationRepo();
        conversations.AddConversation(1, "Chat");
        conversations.AddMessage(1, "system", "Mode changed");
        var svc = CreateService(conversations);

        var md = await svc.ExportMarkdownAsync(1);

        Assert.Contains("### System", md);
        Assert.Contains("Mode changed", md);
    }

    [Fact]
    public async Task ExportMarkdownAsync_CleansUpSwitchModelResults()
    {
        var conversations = new FakeConversationRepo();
        conversations.AddConversation(1, "Chat");
        conversations.AddMessage(1, "user", "[switch_model result]: SUCCESS — now using GPT (gpt-4o).");
        var svc = CreateService(conversations);

        var md = await svc.ExportMarkdownAsync(1);

        Assert.Contains("Switched model:", md);
        Assert.DoesNotContain("[switch_model result]: SUCCESS", md);
    }

    [Fact]
    public async Task ExportMarkdownAsync_NotesImageAttachments()
    {
        var conversations = new FakeConversationRepo();
        conversations.AddConversation(1, "Chat");
        conversations.AddMessageWithImage(1, "user", "Look at this", "/screenshots/img.png");
        var svc = CreateService(conversations);

        var md = await svc.ExportMarkdownAsync(1);

        Assert.Contains("Image attached", md);
        Assert.Contains("img.png", md);
    }

    [Fact]
    public async Task ExportToFileAsync_WritesFile()
    {
        var conversations = new FakeConversationRepo();
        conversations.AddConversation(1, "Test");
        var svc = CreateService(conversations);
        var path = Path.Combine(Path.GetTempPath(), $"aire-export-test-{Guid.NewGuid()}.md");

        try
        {
            await svc.ExportToFileAsync(1, path);
            Assert.True(File.Exists(path));
            var content = await File.ReadAllTextAsync(path);
            Assert.Contains("# Test", content);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static ConversationExportService CreateService(
        FakeConversationRepo? conversations = null,
        FakeProviderRepo? providers = null)
        => new(conversations ?? new FakeConversationRepo(), providers ?? new FakeProviderRepo());

    private sealed class FakeConversationRepo : IConversationRepository
    {
        private readonly List<Conversation> _conversations = [];
        private readonly List<Message> _messages = [];
        private int _nextId = 1;

        public Conversation AddConversation(int providerId, string title)
        {
            var c = new Conversation
            {
                Id = _nextId++,
                ProviderId = providerId,
                Title = title,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
            _conversations.Add(c);
            return c;
        }

        public Message AddMessage(int conversationId, string role, string content)
        {
            var m = new Message
            {
                Id = _messages.Count + 1,
                ConversationId = conversationId,
                Role = role,
                Content = content,
                CreatedAt = DateTime.Now
            };
            _messages.Add(m);
            return m;
        }

        public Task<Conversation?> GetConversationAsync(int id)
            => Task.FromResult(_conversations.Find(c => c.Id == id));

        public Task<List<Message>> GetMessagesAsync(int conversationId)
            => Task.FromResult(_messages.Where(m => m.ConversationId == conversationId).ToList());

        public Task<int> CreateConversationAsync(int providerId, string title) => Task.FromResult(AddConversation(providerId, title).Id);
        public Task<Aire.Data.Conversation?> GetLatestConversationAsync(int providerId) => Task.FromResult<Aire.Data.Conversation?>(null);
        public Task<List<ConversationSummary>> ListConversationsAsync(string? search = null) => Task.FromResult(new List<ConversationSummary>());
        public Task UpdateConversationTitleAsync(int id, string title) => Task.CompletedTask;
        public Task UpdateConversationProviderAsync(int id, int providerId) => Task.CompletedTask;
        public Task UpdateConversationAssistantModeAsync(int id, string key) => Task.CompletedTask;
        public Task SaveMessageAsync(int id, string role, string content, string? imagePath = null, IEnumerable<MessageAttachment>? attachments = null, int? tokens = null) => Task.CompletedTask;
        public Message AddMessageWithImage(int conversationId, string role, string content, string imagePath)
        {
            var m = new Message
            {
                Id = _messages.Count + 1,
                ConversationId = conversationId,
                Role = role,
                Content = content,
                ImagePath = imagePath,
                CreatedAt = DateTime.Now
            };
            _messages.Add(m);
            return m;
        }

        public Task DeleteMessagesByConversationIdAsync(int id) => Task.CompletedTask;
        public Task DeleteConversationAsync(int id) => Task.CompletedTask;
        public Task DeleteAllConversationsAsync() => Task.CompletedTask;
    }

    private sealed class FakeProviderRepo : IProviderRepository
    {
        private readonly List<Provider> _providers = [];

        public Provider Add(Provider provider) { _providers.Add(provider); return provider; }
        public Task<List<Provider>> GetProvidersAsync() => Task.FromResult(_providers.ToList());
        public Task UpdateProviderAsync(Provider provider) => Task.CompletedTask;
        public Task SaveProviderOrderAsync(IEnumerable<Provider> orderedProviders) => Task.CompletedTask;
        public Task<int> InsertProviderAsync(Provider provider) => Task.FromResult(provider.Id);
        public Task DeleteProviderAsync(int id) => Task.CompletedTask;
    }
}
