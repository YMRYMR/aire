using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Aire.Data;
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
}
