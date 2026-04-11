using Aire.AppLayer.Abstractions;
using Aire.AppLayer.Chat;
using Aire.Data;
using Xunit;

namespace Aire.Tests.Services;

public sealed class ConversationApplicationServiceTests
{
    private readonly StubConversationRepository _repo = new();
    private readonly ConversationApplicationService _service;

    public ConversationApplicationServiceTests()
    {
        _service = new ConversationApplicationService(_repo);
    }

    [Fact]
    public async Task GetMessagesAsync_ReturnsMessagesFromRepo()
    {
        _repo.AddMessage(1, "user", "hello");

        var messages = await _service.GetMessagesAsync(1);

        Assert.Single(messages);
        Assert.Equal("hello", messages[0].Content);
    }

    [Fact]
    public async Task CreateConversationAsync_DelegatesToRepo()
    {
        var id = await _service.CreateConversationAsync(42, "Test");

        Assert.True(id > 0);
        Assert.Equal(42, _repo.LastCreatedProviderId);
        Assert.Equal("Test", _repo.LastCreatedTitle);
    }

    [Fact]
    public async Task ListConversationsAsync_ReturnsSummaries()
    {
        _repo.AddConversation(1, 10, "Chat A");
        _repo.AddConversation(2, 10, "Chat B");

        var list = await _service.ListConversationsAsync();

        Assert.Equal(2, list.Count);
    }

    [Fact]
    public async Task ListConversationsAsync_WithSearch_FiltersResults()
    {
        _repo.AddConversation(1, 10, "Alpha Chat");
        _repo.AddConversation(2, 10, "Beta Discussion");

        var list = await _service.ListConversationsAsync("Alpha");

        Assert.Single(list);
        Assert.Equal("Alpha Chat", list[0].Title);
    }

    [Fact]
    public async Task RenameConversationAsync_CallsRepo()
    {
        _repo.AddConversation(5, 1, "Old");
        await _service.RenameConversationAsync(5, "New");

        Assert.Equal("New", _repo.GetRenamedTitle(5));
    }

    [Fact]
    public async Task UpdateConversationAssistantModeAsync_CallsRepo()
    {
        _repo.AddConversation(7, 1, "Chat");
        await _service.UpdateConversationAssistantModeAsync(7, "developer");

        Assert.Equal("developer", _repo.GetAssistantMode(7));
    }

    [Fact]
    public async Task DeleteConversationAsync_CallsBothDeleteMethods()
    {
        _repo.AddConversation(3, 1, "To Delete");
        _repo.AddMessage(3, "user", "bye");

        await _service.DeleteConversationAsync(3);

        Assert.Contains(3, _repo.DeletedMessageConversationIds);
        Assert.Contains(3, _repo.DeletedConversationIds);
    }

    [Fact]
    public async Task DeleteAllConversationsAsync_CallsRepo()
    {
        _repo.AddConversation(1, 1, "A");
        _repo.AddConversation(2, 1, "B");

        await _service.DeleteAllConversationsAsync();

        Assert.True(_repo.AllDeleted);
    }

    [Fact]
    public async Task GetConversationAsync_ReturnsConversation()
    {
        _repo.AddConversation(10, 5, "Found");

        var conv = await _service.GetConversationAsync(10);

        Assert.NotNull(conv);
        Assert.Equal("Found", conv!.Title);
    }

    [Fact]
    public async Task GetConversationAsync_ReturnsNullWhenNotFound()
    {
        var conv = await _service.GetConversationAsync(999);

        Assert.Null(conv);
    }

    private sealed class StubConversationRepository : IConversationRepository
    {
        private readonly List<ConversationSummary> _summaries = [];
        private readonly Dictionary<int, Conversation> _conversations = [];
        private readonly Dictionary<int, List<Aire.Data.Message>> _messages = [];
        private readonly Dictionary<int, string> _renamedTitles = [];
        private readonly Dictionary<int, string> _assistantModes = [];
        private readonly List<int> _deletedMessageConversationIds = [];
        private readonly List<int> _deletedConversationIds = [];

        public int LastCreatedProviderId { get; private set; }
        public string? LastCreatedTitle { get; private set; }
        public List<int> DeletedMessageConversationIds => _deletedMessageConversationIds;
        public List<int> DeletedConversationIds => _deletedConversationIds;
        public bool AllDeleted { get; private set; }

        public void AddConversation(int id, int providerId, string title)
        {
            var conv = new Conversation { Id = id, ProviderId = providerId, Title = title };
            _conversations[id] = conv;
            _summaries.Add(new ConversationSummary { Id = id, Title = title });
        }

        public void AddMessage(int conversationId, string role, string content)
        {
            if (!_messages.ContainsKey(conversationId))
                _messages[conversationId] = [];
            _messages[conversationId].Add(new Aire.Data.Message { ConversationId = conversationId, Role = role, Content = content });
        }

        public string? GetRenamedTitle(int id) => _renamedTitles.GetValueOrDefault(id);
        public string? GetAssistantMode(int id) => _assistantModes.GetValueOrDefault(id);

        public Task<int> CreateConversationAsync(int providerId, string title)
        {
            LastCreatedProviderId = providerId;
            LastCreatedTitle = title;
            var id = _conversations.Count + 1;
            AddConversation(id, providerId, title);
            return Task.FromResult(id);
        }

        public Task<Conversation?> GetLatestConversationAsync(int providerId)
            => Task.FromResult(_conversations.Values.LastOrDefault(c => c.ProviderId == providerId));

        public Task<Conversation?> GetConversationAsync(int conversationId)
            => Task.FromResult(_conversations.GetValueOrDefault(conversationId));

        public Task<List<ConversationSummary>> ListConversationsAsync(string? search = null)
        {
            var results = string.IsNullOrEmpty(search)
                ? _summaries
                : _summaries.Where(s => s.Title?.Contains(search, StringComparison.OrdinalIgnoreCase) == true).ToList();
            return Task.FromResult(results);
        }

        public Task UpdateConversationTitleAsync(int conversationId, string title)
        {
            _renamedTitles[conversationId] = title;
            return Task.CompletedTask;
        }

        public Task UpdateConversationProviderAsync(int conversationId, int providerId)
            => Task.CompletedTask;

        public Task UpdateConversationAssistantModeAsync(int conversationId, string assistantModeKey)
        {
            _assistantModes[conversationId] = assistantModeKey;
            return Task.CompletedTask;
        }

        public Task SaveMessageAsync(int conversationId, string role, string content,
            string? imagePath = null, IEnumerable<MessageAttachment>? attachments = null, int? tokens = null)
        {
            AddMessage(conversationId, role, content);
            return Task.CompletedTask;
        }

        public Task<List<Aire.Data.Message>> GetMessagesAsync(int conversationId)
            => Task.FromResult(_messages.GetValueOrDefault(conversationId) ?? []);

        public Task DeleteMessagesByConversationIdAsync(int conversationId)
        {
            _deletedMessageConversationIds.Add(conversationId);
            _messages.Remove(conversationId);
            return Task.CompletedTask;
        }

        public Task DeleteConversationAsync(int conversationId)
        {
            _deletedConversationIds.Add(conversationId);
            _conversations.Remove(conversationId);
            _summaries.RemoveAll(s => s.Id == conversationId);
            return Task.CompletedTask;
        }

        public Task DeleteAllConversationsAsync()
        {
            AllDeleted = true;
            _conversations.Clear();
            _summaries.Clear();
            _messages.Clear();
            return Task.CompletedTask;
        }
    }
}
