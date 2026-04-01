using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Aire.Data;
using Aire.Services.Mcp;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Aire.Tests.Services;

public class DatabaseServiceConversationAndMcpTests : IAsyncLifetime, IDisposable
{
    private readonly string _dbPath;

    private readonly DatabaseService _db;

    public DatabaseServiceConversationAndMcpTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"aire_db_cov_{Guid.NewGuid():N}.db");
        _db = new DatabaseService(_dbPath);
    }

    public async Task InitializeAsync()
    {
        await _db.InitializeAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _db.Dispose();
        SqliteConnection.ClearAllPools();
        try
        {
            if (File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }
        }
        catch
        {
        }
    }

    [Fact]
    public async Task ConversationCrud_RoundTripsMessagesAndSearch()
    {
        int providerId = await _db.InsertProviderAsync(new Provider
        {
            Name = "Coverage Provider",
            Type = "OpenAI",
            ApiKey = "sk-test",
            Model = "gpt-4o",
            IsEnabled = true,
            Color = "#123456"
        });
        int conversationId = await _db.CreateConversationAsync(providerId, "Coverage Chat");
        await _db.SaveMessageAsync(conversationId, "user", "hello world");
        await _db.SaveMessageAsync(conversationId, "assistant", "response text", "image.png");
        Conversation latest = await _db.GetLatestConversationAsync(providerId);
        Conversation conversation = await _db.GetConversationAsync(conversationId);
        List<Message> messages = await _db.GetMessagesAsync(conversationId);
        List<ConversationSummary> listed = await _db.ListConversationsAsync();
        List<ConversationSummary> searched = await _db.ListConversationsAsync("hello");
        Assert.NotNull(latest);
        Assert.NotNull(conversation);
        Assert.Equal("Coverage Chat", conversation.Title);
        Assert.Equal(2, messages.Count);
        Assert.Equal("image.png", messages[1].ImagePath);
        Assert.Contains((IEnumerable<ConversationSummary>)listed, (Predicate<ConversationSummary>)((ConversationSummary c) => c.Id == conversationId && c.ProviderName == "Coverage Provider"));
        Assert.Contains((IEnumerable<ConversationSummary>)searched, (Predicate<ConversationSummary>)((ConversationSummary c) => c.Id == conversationId));
        await _db.UpdateConversationTitleAsync(conversationId, "Renamed");
        await _db.UpdateConversationProviderAsync(conversationId, providerId);
        Assert.Equal("Renamed", (await _db.GetConversationAsync(conversationId)).Title);
        await _db.DeleteMessagesByConversationIdAsync(conversationId);
        Assert.Empty(await _db.GetMessagesAsync(conversationId));
        await _db.DeleteConversationAsync(conversationId);
        Assert.Null(await _db.GetConversationAsync(conversationId));
    }

    [Fact]
    public async Task McpServerCrud_EncryptsAndRestoresEnvVars()
    {
        McpServerConfig config = new McpServerConfig
        {
            Name = "Docs",
            Command = "cmd",
            Arguments = "/c echo hi",
            WorkingDirectory = "C:/repo",
            EnvVars = new Dictionary<string, string>
            {
                ["TOKEN"] = "secret",
                ["MODE"] = "dev"
            },
            IsEnabled = true,
            SortOrder = 2
        };
        int id = await _db.InsertMcpServerAsync(config);
        McpServerConfig saved = Assert.Single(await _db.GetMcpServersAsync());
        Assert.Equal(id, saved.Id);
        Assert.Equal("secret", saved.EnvVars["TOKEN"]);
        config.Id = id;
        config.Name = "Docs Updated";
        config.IsEnabled = false;
        config.EnvVars["TOKEN"] = "changed";
        await _db.UpdateMcpServerAsync(config);
        McpServerConfig updated = Assert.Single(await _db.GetMcpServersAsync());
        Assert.Equal("Docs Updated", updated.Name);
        Assert.False(updated.IsEnabled);
        Assert.Equal("changed", updated.EnvVars["TOKEN"]);
        using SqliteConnection connection = new SqliteConnection("Data Source=" + _dbPath);
        await connection.OpenAsync();
        using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT EnvVarsJson FROM McpServers WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        string raw = (string)(await cmd.ExecuteScalarAsync());
        Assert.Contains("dpapi:", raw, StringComparison.OrdinalIgnoreCase);
        await _db.DeleteMcpServerAsync(id);
        Assert.Empty(await _db.GetMcpServersAsync());
    }
}
