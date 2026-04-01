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

public sealed class DatabaseServiceAdditionalCoverageTests : IAsyncLifetime, IDisposable
{
    private readonly string _dbPath;

    private readonly DatabaseService _db;

    public DatabaseServiceAdditionalCoverageTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"aire_db_additional_{Guid.NewGuid():N}.db");
        _db = new DatabaseService(_dbPath);
    }

    public Task InitializeAsync()
    {
        return _db.InitializeAsync();
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
    public async Task SaveProviderOrderAsync_PersistsRequestedOrder()
    {
        List<Provider> reordered = (await _db.GetProvidersAsync()).OrderByDescending((Provider provider) => provider.Id).ToList();
        await _db.SaveProviderOrderAsync(reordered);
        List<Provider> reloaded = await _db.GetProvidersAsync();
        Assert.Equal(reordered.Select((Provider provider) => provider.Id), reloaded.Select((Provider provider) => provider.Id));
        Assert.Equal(Enumerable.Range(0, reloaded.Count), reloaded.Select((Provider provider) => provider.SortOrder));
    }

    [Fact]
    public async Task EmailAccountCrud_RoundTripsSecretsAndDeleteRemovesRow()
    {
        EmailAccount account = new EmailAccount
        {
            DisplayName = "Coverage Inbox",
            Provider = EmailProvider.Custom,
            ImapHost = "imap.example.test",
            ImapPort = 1993,
            SmtpHost = "smtp.example.test",
            SmtpPort = 1587,
            Username = "coverage@example.test",
            PlaintextPassword = "pw-insert",
            UseOAuth = true,
            OAuthRefreshToken = "refresh-insert",
            IsEnabled = true
        };
        int id = await _db.InsertEmailAccountAsync(account);
        EmailAccount inserted = Assert.Single(await _db.GetEmailAccountsAsync());
        Assert.Equal(id, inserted.Id);
        Assert.Equal("Coverage Inbox", inserted.DisplayName);
        Assert.True(inserted.UseOAuth);
        Assert.Equal("pw-insert", SecureStorage.Unprotect(inserted.EncryptedPassword));
        Assert.Equal("refresh-insert", SecureStorage.Unprotect(inserted.OAuthRefreshToken));
        inserted.DisplayName = "Coverage Inbox Updated";
        inserted.Provider = EmailProvider.Outlook;
        inserted.ImapHost = "outlook-imap.example.test";
        inserted.ImapPort = 2993;
        inserted.SmtpHost = "outlook-smtp.example.test";
        inserted.SmtpPort = 2587;
        inserted.Username = "updated@example.test";
        inserted.PlaintextPassword = "pw-updated";
        inserted.UseOAuth = false;
        inserted.OAuthRefreshToken = "refresh-updated";
        inserted.IsEnabled = false;
        await _db.UpdateEmailAccountAsync(inserted);
        EmailAccount updated = Assert.Single(await _db.GetEmailAccountsAsync());
        Assert.Equal("Coverage Inbox Updated", updated.DisplayName);
        Assert.Equal(EmailProvider.Outlook, updated.Provider);
        Assert.Equal("outlook-imap.example.test", updated.ImapHost);
        Assert.Equal(2993, updated.ImapPort);
        Assert.Equal("outlook-smtp.example.test", updated.SmtpHost);
        Assert.Equal(2587, updated.SmtpPort);
        Assert.Equal("updated@example.test", updated.Username);
        Assert.False(updated.IsEnabled);
        Assert.False(updated.UseOAuth);
        Assert.Equal("pw-updated", SecureStorage.Unprotect(updated.EncryptedPassword));
        Assert.Equal("refresh-updated", SecureStorage.Unprotect(updated.OAuthRefreshToken));
        await _db.DeleteEmailAccountAsync(id);
        Assert.Empty(await _db.GetEmailAccountsAsync());
    }

    [Fact]
    public async Task DeleteAllConversationsAsync_RemovesConversationsAndMessages()
    {
        int providerId = await _db.InsertProviderAsync(new Provider
        {
            Name = "Delete All Coverage",
            Type = "OpenAI",
            ApiKey = "sk-delete-all",
            Model = "gpt-4o-mini",
            IsEnabled = true,
            Color = "#123123"
        });
        int firstConversationId = await _db.CreateConversationAsync(providerId, "First");
        int secondConversationId = await _db.CreateConversationAsync(providerId, "Second");
        await _db.SaveMessageAsync(firstConversationId, "user", "hello first");
        await _db.SaveMessageAsync(secondConversationId, "assistant", "hello second");
        await _db.DeleteAllConversationsAsync();
        Assert.Empty(await _db.ListConversationsAsync());
        Assert.Null(await _db.GetConversationAsync(firstConversationId));
        Assert.Null(await _db.GetConversationAsync(secondConversationId));
        Assert.Empty(await _db.GetMessagesAsync(firstConversationId));
        Assert.Empty(await _db.GetMessagesAsync(secondConversationId));
        Assert.Null(await _db.GetLatestConversationAsync(providerId));
    }

    [Fact]
    public async Task LogFileAccessAsync_WritesExpectedAuditRow()
    {
        await _db.LogFileAccessAsync("write", "C:\\temp\\coverage.txt", allowed: true);
        using SqliteConnection connection = new SqliteConnection("Data Source=" + _dbPath);
        await connection.OpenAsync();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "\r\n            SELECT Operation, Path, Allowed, ApprovedAt\r\n            FROM FileAccessLog";
        using SqliteDataReader reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("write", reader.GetString(0));
        Assert.Equal("C:\\temp\\coverage.txt", reader.GetString(1));
        Assert.True(reader.GetBoolean(2));
        Assert.False(reader.IsDBNull(3));
        Assert.False(await reader.ReadAsync());
    }
}
