using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Aire.Data;
using Aire.Services;
using Aire.Services.Email;
using Aire.Services.Tools;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Aire.Tests.Services;

public class EmailToolServiceCoverageTests : IAsyncLifetime, IDisposable
{
    private readonly string _dbPath;

    private readonly DatabaseService _db;

    private readonly EmailToolService _service;

    public EmailToolServiceCoverageTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"aire_email_test_{Guid.NewGuid():N}.db");
        _db = new DatabaseService(_dbPath);
        _service = new EmailToolService(_db);
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
    public async Task RefreshIsConfiguredAsync_TracksEnabledAccounts()
    {
        await _db.InsertEmailAccountAsync(new EmailAccount
        {
            DisplayName = "Disabled",
            Provider = EmailProvider.Custom,
            ImapHost = "imap.example.com",
            SmtpHost = "smtp.example.com",
            Username = "disabled@example.com",
            EncryptedPassword = (SecureStorage.Protect("pw") ?? string.Empty),
            IsEnabled = false
        });
        await _service.RefreshIsConfiguredAsync();
        Assert.False(_service.IsConfigured);
        await _db.InsertEmailAccountAsync(new EmailAccount
        {
            DisplayName = "Enabled",
            Provider = EmailProvider.Custom,
            ImapHost = "imap.example.com",
            SmtpHost = "smtp.example.com",
            Username = "enabled@example.com",
            EncryptedPassword = (SecureStorage.Protect("pw") ?? string.Empty),
            IsEnabled = true
        });
        await _service.RefreshIsConfiguredAsync();
        Assert.True(_service.IsConfigured);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownTool_ReturnsUnknownMessage()
    {
        Assert.Contains("Unknown email tool", (await _service.ExecuteAsync(new ToolCallRequest
        {
            Tool = "not_email"
        })).TextResult);
    }

    [Fact]
    public async Task ExecuteAsync_ReadEmailsWithoutConfiguredAccount_ReturnsFriendlyError()
    {
        Assert.Contains("No email account configured", (await _service.ExecuteAsync(new ToolCallRequest
        {
            Tool = "read_emails",
            Parameters = JsonDocument.Parse("{}").RootElement.Clone()
        })).TextResult);
    }

    [Fact]
    public async Task ExecuteAsync_SendSearchAndReply_ValidateRequiredParameters()
    {
        ToolExecutionResult send = await _service.ExecuteAsync(new ToolCallRequest
        {
            Tool = "send_email",
            Parameters = JsonDocument.Parse("{\"subject\":\"s\",\"body\":\"b\"}").RootElement.Clone()
        });
        ToolExecutionResult search = await _service.ExecuteAsync(new ToolCallRequest
        {
            Tool = "search_emails",
            Parameters = JsonDocument.Parse("{}").RootElement.Clone()
        });
        ToolExecutionResult reply = await _service.ExecuteAsync(new ToolCallRequest
        {
            Tool = "reply_to_email",
            Parameters = JsonDocument.Parse("{\"body\":\"hello\"}").RootElement.Clone()
        });
        Assert.Contains("'to' parameter is required", send.TextResult);
        Assert.Contains("'query' parameter is required", search.TextResult);
        Assert.Contains("'message_id' is required", reply.TextResult);
    }
}
