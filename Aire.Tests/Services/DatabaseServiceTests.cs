using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Aire.Data;
using Aire.Services;
using Aire.Services.Email;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Aire.Tests.Services;

public class DatabaseServiceTests : IAsyncLifetime, IDisposable
{
    private readonly string _dbPath;

    private readonly DatabaseService _db;

    public DatabaseServiceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"aire_test_{Guid.NewGuid():N}.db");
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
    public async Task InitializeAsync_SeedsDefaultProviders()
    {
        List<Provider> providers = await _db.GetProvidersAsync();
        Assert.NotEmpty(providers);
        Assert.Equal(6, providers.Count);
    }

    [Fact]
    public async Task GetProvidersAsync_ReturnsSeededProviders()
    {
        List<Provider> providers = await _db.GetProvidersAsync();
        Assert.Contains((IEnumerable<Provider>)providers, (Predicate<Provider>)((Provider p) => p.Type == "OpenAI"));
        Assert.Contains((IEnumerable<Provider>)providers, (Predicate<Provider>)((Provider p) => p.Type == "Inception"));
        Assert.Contains((IEnumerable<Provider>)providers, (Predicate<Provider>)((Provider p) => p.Type == "DeepSeek"));
        Assert.Contains((IEnumerable<Provider>)providers, (Predicate<Provider>)((Provider p) => p.Type == "Anthropic"));
        Assert.Contains((IEnumerable<Provider>)providers, (Predicate<Provider>)((Provider p) => p.Type == "Ollama" && p.Model == "qwen2.5-coder:7b"));
    }

    [Fact]
    public async Task InsertProviderAsync_AddsNewProvider()
    {
        List<Provider> before = await _db.GetProvidersAsync();
        Provider newProvider = new Provider
        {
            Name = "Test Provider",
            Type = "OpenAI",
            ApiKey = "sk-test",
            Model = "gpt-4o",
            IsEnabled = true,
            Color = "#AAAAAA"
        };
        Assert.True(await _db.InsertProviderAsync(newProvider) > 0);
        List<Provider> after = await _db.GetProvidersAsync();
        Assert.Equal(before.Count + 1, after.Count);
        Assert.Contains((IEnumerable<Provider>)after, (Predicate<Provider>)((Provider p) => p.Name == "Test Provider"));
    }

    [Fact]
    public async Task UpdateProviderAsync_ChangesProvider()
    {
        Provider first = (await _db.GetProvidersAsync()).First();
        first.Name = "Updated Name";
        first.ApiKey = "sk-updated";
        await _db.UpdateProviderAsync(first);
        Assert.Contains((IEnumerable<Provider>)(await _db.GetProvidersAsync()), (Predicate<Provider>)((Provider p) => p.Name == "Updated Name" && p.Id == first.Id));
    }

    [Fact]
    public async Task DeleteProviderAsync_RemovesProvider()
    {
        Provider newProvider = new Provider
        {
            Name = "To Delete",
            Type = "OpenAI",
            Model = "gpt-4o",
            IsEnabled = true,
            Color = "#000000"
        };
        int id = await _db.InsertProviderAsync(newProvider);
        await _db.DeleteProviderAsync(id);
        Assert.DoesNotContain((IEnumerable<Provider>)(await _db.GetProvidersAsync()), (Predicate<Provider>)((Provider p) => p.Id == id));
    }

    [Fact]
    public async Task GetSettingAsync_ReturnsNullForMissingKey()
    {
        Assert.Null(await _db.GetSettingAsync("nonexistent_key"));
    }

    [Fact]
    public async Task SetSettingAsync_PersistsValue()
    {
        await _db.SetSettingAsync("theme", "dark");
        Assert.Equal("dark", await _db.GetSettingAsync("theme"));
    }

    [Fact]
    public async Task InitializeAsync_MigratesClaudeSessionProvidersToClaudeWeb()
    {
        int insertedId = await _db.InsertProviderAsync(new Provider
        {
            Name = "Claude.ai Session",
            Type = "Anthropic",
            ApiKey = "claude.ai-session",
            Model = "claude-sonnet-4-5",
            IsEnabled = true,
            Color = "#111111"
        });
        using DatabaseService db2 = new DatabaseService(_dbPath);
        await db2.InitializeAsync();
        Provider migrated = Assert.Single((await db2.GetProvidersAsync()).Where((Provider p) => p.Id == insertedId));
        Assert.Equal("ClaudeWeb", migrated.Type);
        Assert.Equal("claude.ai-session", migrated.ApiKey);
        Assert.Null(migrated.BaseUrl);
    }

    [Fact]
    public async Task SetSettingAsync_OverwritesExistingValue()
    {
        await _db.SetSettingAsync("theme", "dark");
        await _db.SetSettingAsync("theme", "light");
        Assert.Equal("light", await _db.GetSettingAsync("theme"));
    }

    [Fact]
    public async Task SaveProviderOrderAsync_PersistsRequestedOrdering()
    {
        int firstId = await _db.InsertProviderAsync(new Provider
        {
            Name = "Order A",
            Type = "OpenAI",
            ApiKey = "sk-a",
            Model = "gpt-4o",
            IsEnabled = true,
            Color = "#111111"
        });
        int secondId = await _db.InsertProviderAsync(new Provider
        {
            Name = "Order B",
            Type = "OpenAI",
            ApiKey = "sk-b",
            Model = "gpt-4o",
            IsEnabled = true,
            Color = "#222222"
        });
        List<Provider> providers = await _db.GetProvidersAsync();
        Provider first = providers.Single((Provider p) => p.Id == firstId);
        Provider second = providers.Single((Provider p) => p.Id == secondId);
        await _db.SaveProviderOrderAsync(new Provider[2] { second, first });
        List<Provider> reordered = await _db.GetProvidersAsync();
        Assert.True(reordered.FindIndex((Provider p) => p.Id == secondId) < reordered.FindIndex((Provider p) => p.Id == firstId));
    }

    [Fact]
    public async Task DeleteAllConversationsAsync_RemovesConversationsAndMessages()
    {
        int providerId = await _db.InsertProviderAsync(new Provider
        {
            Name = "Chat Provider",
            Type = "OpenAI",
            ApiKey = "sk-chat",
            Model = "gpt-4o",
            IsEnabled = true,
            Color = "#333333"
        });
        int conversationId = await _db.CreateConversationAsync(providerId, "Delete me");
        await _db.SaveMessageAsync(conversationId, "user", "first");
        await _db.SaveMessageAsync(conversationId, "assistant", "second");
        await _db.DeleteAllConversationsAsync();
        Assert.Empty(await _db.ListConversationsAsync());
        Assert.Empty(await _db.GetMessagesAsync(conversationId));
        Assert.Null(await _db.GetLatestConversationAsync(providerId));
    }

    [Fact]
    public async Task EmailAccountCrud_RoundTripsEncryptedSecrets()
    {
        EmailAccount account = new EmailAccount
        {
            DisplayName = "Coverage Mail",
            Provider = EmailProvider.Gmail,
            ImapHost = "imap.example.com",
            ImapPort = 993,
            SmtpHost = "smtp.example.com",
            SmtpPort = 587,
            Username = "user@example.com",
            PlaintextPassword = "pw-1",
            IsEnabled = true,
            UseOAuth = true,
            OAuthRefreshToken = "refresh-1"
        };
        int id = await _db.InsertEmailAccountAsync(account);
        EmailAccount stored = Assert.Single((await _db.GetEmailAccountsAsync()).Where((EmailAccount a) => a.Id == id));
        Assert.Equal("Coverage Mail", stored.DisplayName);
        Assert.True(stored.UseOAuth);
        Assert.Equal("refresh-1", SecureStorage.Unprotect(stored.OAuthRefreshToken));
        Assert.NotEqual("pw-1", stored.EncryptedPassword);
        account.Id = id;
        account.DisplayName = "Updated Mail";
        account.PlaintextPassword = "pw-2";
        account.OAuthRefreshToken = "refresh-2";
        account.IsEnabled = false;
        await _db.UpdateEmailAccountAsync(account);
        EmailAccount updated = Assert.Single((await _db.GetEmailAccountsAsync()).Where((EmailAccount a) => a.Id == id));
        Assert.Equal("Updated Mail", updated.DisplayName);
        Assert.False(updated.IsEnabled);
        Assert.Equal("refresh-2", SecureStorage.Unprotect(updated.OAuthRefreshToken));
        await _db.DeleteEmailAccountAsync(id);
        Assert.DoesNotContain((IEnumerable<EmailAccount>)(await _db.GetEmailAccountsAsync()), (Predicate<EmailAccount>)((EmailAccount a) => a.Id == id));
    }

    [Fact]
    public async Task LogFileAccessAsync_WritesAuditRow()
    {
        await _db.LogFileAccessAsync("read", "C:/tmp/file.txt", allowed: true);
        using SqliteConnection connection = new SqliteConnection("Data Source=" + _dbPath);
        await connection.OpenAsync();
        using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Operation, Path, Allowed FROM FileAccessLog ORDER BY Id DESC LIMIT 1";
        using SqliteDataReader reader = await cmd.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("read", reader.GetString(0));
        Assert.Equal("C:/tmp/file.txt", reader.GetString(1));
        Assert.True(reader.GetBoolean(2));
    }
}
