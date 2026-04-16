using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Aire.Data;
using Xunit;

namespace Aire.Tests.Services;

public sealed class ConversationBranchingTests : IAsyncLifetime, IDisposable
{
    private readonly string _dbPath;
    private readonly DatabaseService _db;

    public ConversationBranchingTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"aire_branch_test_{System.Guid.NewGuid():N}.db");
        _db = new DatabaseService(_dbPath);
    }

    public async Task InitializeAsync() => await _db.InitializeAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        _db.Dispose();
        try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { }
    }

    private async Task<(int providerId, int conversationId)> SeedConversationAsync()
    {
        int providerId = await _db.InsertProviderAsync(new Provider
        {
            Name = "Test", Type = "OpenAI", ApiKey = "sk-test",
            Model = "gpt-4o-mini", IsEnabled = true, Color = "#000"
        });

        int conversationId = await _db.CreateConversationAsync(providerId, "Branch test");
        return (providerId, conversationId);
    }

    [Fact]
    public async Task BranchConversationAsync_CopiesMessagesUpToBranchPoint()
    {
        var (_, conversationId) = await SeedConversationAsync();

        await _db.SaveMessageAsync(conversationId, "user", "msg 1");
        await _db.SaveMessageAsync(conversationId, "assistant", "msg 2");
        await _db.SaveMessageAsync(conversationId, "user", "msg 3");
        await _db.SaveMessageAsync(conversationId, "assistant", "msg 4");

        var messages = await _db.GetMessagesAsync(conversationId);
        var branchPointId = messages[1].Id;

        var branchedId = await _db.BranchConversationAsync(conversationId, branchPointId);

        var branchedMessages = await _db.GetMessagesAsync(branchedId);
        Assert.Equal(2, branchedMessages.Count);
        Assert.Equal("msg 1", branchedMessages[0].Content);
        Assert.Equal("msg 2", branchedMessages[1].Content);
    }

    [Fact]
    public async Task BranchConversationAsync_PreservesRoles()
    {
        var (_, conversationId) = await SeedConversationAsync();

        await _db.SaveMessageAsync(conversationId, "user", "hello");
        await _db.SaveMessageAsync(conversationId, "assistant", "world");

        var messages = await _db.GetMessagesAsync(conversationId);
        var branchedId = await _db.BranchConversationAsync(conversationId, messages[0].Id);

        var branched = await _db.GetMessagesAsync(branchedId);
        Assert.Single(branched);
        Assert.Equal("user", branched[0].Role);
        Assert.Equal("hello", branched[0].Content);
    }

    [Fact]
    public async Task BranchConversationAsync_TitleHasBranchSuffix()
    {
        var (_, conversationId) = await SeedConversationAsync();
        await _db.SaveMessageAsync(conversationId, "user", "hello");

        var messages = await _db.GetMessagesAsync(conversationId);
        var branchedId = await _db.BranchConversationAsync(conversationId, messages[0].Id);

        var branched = await _db.GetConversationAsync(branchedId);
        Assert.NotNull(branched);
        Assert.Contains("branch", branched.Title, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BranchConversationAsync_SameProvider()
    {
        var (providerId, conversationId) = await SeedConversationAsync();
        await _db.SaveMessageAsync(conversationId, "user", "hello");

        var messages = await _db.GetMessagesAsync(conversationId);
        var branchedId = await _db.BranchConversationAsync(conversationId, messages[0].Id);

        var branched = await _db.GetConversationAsync(branchedId);
        Assert.NotNull(branched);
        Assert.Equal(providerId, branched.ProviderId);
    }

    [Fact]
    public async Task BranchConversationAsync_ThrowsForMissingConversation()
    {
        await Assert.ThrowsAsync<System.ArgumentException>(
            () => _db.BranchConversationAsync(99999, 1));
    }

    [Fact]
    public async Task BranchConversationAsync_BranchAtLastMessage_CopiesAll()
    {
        var (_, conversationId) = await SeedConversationAsync();

        await _db.SaveMessageAsync(conversationId, "user", "a");
        await _db.SaveMessageAsync(conversationId, "assistant", "b");
        await _db.SaveMessageAsync(conversationId, "user", "c");

        var messages = await _db.GetMessagesAsync(conversationId);
        var branchedId = await _db.BranchConversationAsync(conversationId, messages[^1].Id);

        var branched = await _db.GetMessagesAsync(branchedId);
        Assert.Equal(3, branched.Count);
    }

    [Fact]
    public async Task BranchConversationAsync_OriginalUnmodified()
    {
        var (_, conversationId) = await SeedConversationAsync();

        await _db.SaveMessageAsync(conversationId, "user", "a");
        await _db.SaveMessageAsync(conversationId, "assistant", "b");
        await _db.SaveMessageAsync(conversationId, "user", "c");

        var messages = await _db.GetMessagesAsync(conversationId);
        var branchedId = await _db.BranchConversationAsync(conversationId, messages[0].Id);

        var original = await _db.GetMessagesAsync(conversationId);
        Assert.Equal(3, original.Count);

        var branched = await _db.GetMessagesAsync(branchedId);
        Assert.Single(branched);
    }

    [Fact]
    public async Task BranchConversationAsync_PreservesTokens()
    {
        var (_, conversationId) = await SeedConversationAsync();

        await _db.SaveMessageAsync(conversationId, "user", "hello");
        await _db.SaveMessageAsync(conversationId, "assistant", "response", tokens: 42);

        var messages = await _db.GetMessagesAsync(conversationId);
        var branchedId = await _db.BranchConversationAsync(conversationId, messages[1].Id);

        var branched = await _db.GetMessagesAsync(branchedId);
        Assert.Equal(2, branched.Count);
        Assert.Equal(42, branched[1].Tokens);
    }
}
