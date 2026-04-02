using System.Collections.Generic;
using System.Threading.Tasks;
using Aire.AppLayer.Abstractions;
using Aire.AppLayer.Connections;
using Aire.AppLayer.Mcp;
using Aire.AppLayer.Settings;
using Aire.Domain.Providers;
using Aire.Services.Email;
using Aire.Services.Mcp;
using Xunit;

namespace Aire.Tests.Services;

/// <summary>
/// Direct unit tests for the application services introduced during the DB-narrowing refactor.
/// These tests use lightweight in-memory fakes so no database or WPF runtime is required.
/// </summary>
public sealed class ApplicationServiceTests
{
    // ── in-memory fakes ───────────────────────────────────────────────────────

    private sealed class FakeSettingsRepository : ISettingsRepository
    {
        private readonly Dictionary<string, string> _store = new();

        public Task<string?> GetSettingAsync(string key)
            => Task.FromResult(_store.TryGetValue(key, out var v) ? v : (string?)null);

        public Task SetSettingAsync(string key, string value)
        {
            _store[key] = value;
            return Task.CompletedTask;
        }

        public Task LogFileAccessAsync(string operation, string path, bool allowed)
            => Task.CompletedTask;
    }

    private sealed class FakeMcpConfigRepository : IMcpConfigRepository
    {
        private readonly List<McpServerConfig> _configs = new();
        private int _nextId = 1;

        public Task<List<McpServerConfig>> GetMcpServersAsync()
            => Task.FromResult(new List<McpServerConfig>(_configs));

        public Task<int> InsertMcpServerAsync(McpServerConfig config)
        {
            config.Id = _nextId++;
            _configs.Add(config);
            return Task.FromResult(config.Id);
        }

        public Task UpdateMcpServerAsync(McpServerConfig config)
        {
            var idx = _configs.FindIndex(c => c.Id == config.Id);
            if (idx >= 0) _configs[idx] = config;
            return Task.CompletedTask;
        }

        public Task DeleteMcpServerAsync(int id)
        {
            _configs.RemoveAll(c => c.Id == id);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeEmailAccountRepository : IEmailAccountRepository
    {
        private readonly List<EmailAccount> _accounts = new();
        private int _nextId = 1;

        public Task<List<EmailAccount>> GetEmailAccountsAsync()
            => Task.FromResult(new List<EmailAccount>(_accounts));

        public Task<int> InsertEmailAccountAsync(EmailAccount account)
        {
            account.Id = _nextId++;
            _accounts.Add(account);
            return Task.FromResult(account.Id);
        }

        public Task UpdateEmailAccountAsync(EmailAccount account)
        {
            var idx = _accounts.FindIndex(a => a.Id == account.Id);
            if (idx >= 0) _accounts[idx] = account;
            return Task.CompletedTask;
        }

        public Task DeleteEmailAccountAsync(int id)
        {
            _accounts.RemoveAll(a => a.Id == id);
            return Task.CompletedTask;
        }
    }

    // ── ProviderValidationResult ──────────────────────────────────────────────

    [Fact]
    public void ProviderValidationResult_Ok_IsValidWithNullError()
    {
        var result = ProviderValidationResult.Ok();
        Assert.True(result.IsValid);
        Assert.Null(result.Error);
    }

    [Fact]
    public void ProviderValidationResult_Fail_IsNotValidWithMessage()
    {
        var result = ProviderValidationResult.Fail("API key is required.");
        Assert.False(result.IsValid);
        Assert.Equal("API key is required.", result.Error);
    }

    [Fact]
    public void ProviderValidationResult_Fail_EmptyString_IsNotValid()
    {
        var result = ProviderValidationResult.Fail(string.Empty);
        Assert.False(result.IsValid);
        Assert.Equal(string.Empty, result.Error);
    }

    // ── AppSettingsApplicationService ─────────────────────────────────────────

    [Fact]
    public async Task AppSettings_GetSetting_ReturnsNullWhenKeyAbsent()
    {
        var svc = new AppSettingsApplicationService(new FakeSettingsRepository());
        Assert.Null(await svc.GetSettingAsync("missing"));
    }

    [Fact]
    public async Task AppSettings_SaveThenGet_RoundTrips()
    {
        var svc = new AppSettingsApplicationService(new FakeSettingsRepository());
        await svc.SaveSettingAsync("theme", "dark");
        Assert.Equal("dark", await svc.GetSettingAsync("theme"));
    }

    [Fact]
    public async Task AppSettings_OverwriteSetting_ReturnsLatestValue()
    {
        var svc = new AppSettingsApplicationService(new FakeSettingsRepository());
        await svc.SaveSettingAsync("theme", "light");
        await svc.SaveSettingAsync("theme", "dark");
        Assert.Equal("dark", await svc.GetSettingAsync("theme"));
    }

    // ── McpConfigApplicationService ───────────────────────────────────────────

    [Fact]
    public async Task McpConfig_GetMcpServers_ReturnsEmptyWhenNoneConfigured()
    {
        var svc = new McpConfigApplicationService(new FakeMcpConfigRepository());
        Assert.Empty(await svc.GetMcpServersAsync());
    }

    [Fact]
    public async Task McpConfig_Insert_AssignsIdAndReturnsInList()
    {
        var svc = new McpConfigApplicationService(new FakeMcpConfigRepository());
        var id = await svc.InsertMcpServerAsync(new McpServerConfig { Name = "GitHub", Command = "npx" });
        Assert.True(id > 0);
        var list = await svc.GetMcpServersAsync();
        Assert.Single(list);
        Assert.Equal("GitHub", list[0].Name);
    }

    [Fact]
    public async Task McpConfig_Update_PersistsNameChange()
    {
        var repo = new FakeMcpConfigRepository();
        var svc = new McpConfigApplicationService(repo);
        var id = await svc.InsertMcpServerAsync(new McpServerConfig { Name = "Old", Command = "cmd" });
        var inserted = (await svc.GetMcpServersAsync())[0];
        inserted.Name = "New";
        await svc.UpdateMcpServerAsync(inserted);
        Assert.Equal("New", (await svc.GetMcpServersAsync())[0].Name);
    }

    [Fact]
    public async Task McpConfig_Delete_RemovesServer()
    {
        var svc = new McpConfigApplicationService(new FakeMcpConfigRepository());
        var id = await svc.InsertMcpServerAsync(new McpServerConfig { Name = "Temp", Command = "x" });
        await svc.DeleteMcpServerAsync(id);
        Assert.Empty(await svc.GetMcpServersAsync());
    }

    [Fact]
    public void McpCatalog_BuildConfig_UsesCuratedTemplate()
    {
        var svc = new McpCatalogApplicationService();

        var config = svc.BuildConfig("github");

        Assert.Equal("GitHub", config.Name);
        Assert.Equal("npx", config.Command);
        Assert.Contains("@modelcontextprotocol/server-github", config.Arguments);
        Assert.True(config.EnvVars.ContainsKey("GITHUB_PERSONAL_ACCESS_TOKEN"));
        Assert.True(config.IsEnabled);
    }

    [Fact]
    public void McpCatalog_FindInstalledConfig_MatchesCuratedTemplate()
    {
        var svc = new McpCatalogApplicationService();
        var installed = new List<McpServerConfig>
        {
            new()
            {
                Id = 42,
                Name = "Filesystem",
                Command = "npx",
                Arguments = "-y @modelcontextprotocol/server-filesystem"
            }
        };

        var match = svc.FindInstalledConfig("filesystem", installed);

        Assert.NotNull(match);
        Assert.Equal(42, match!.Id);
    }

    // ── EmailAccountApplicationService ───────────────────────────────────────

    [Fact]
    public async Task EmailAccount_GetAccounts_ReturnsEmptyWhenNoneConfigured()
    {
        var svc = new EmailAccountApplicationService(new FakeEmailAccountRepository());
        Assert.Empty(await svc.GetEmailAccountsAsync());
    }

    [Fact]
    public async Task EmailAccount_Insert_AssignsIdAndReturnsInList()
    {
        var svc = new EmailAccountApplicationService(new FakeEmailAccountRepository());
        var id = await svc.InsertEmailAccountAsync(new EmailAccount { DisplayName = "Work", Username = "me@work.com" });
        Assert.True(id > 0);
        var list = await svc.GetEmailAccountsAsync();
        Assert.Single(list);
        Assert.Equal("Work", list[0].DisplayName);
    }

    [Fact]
    public async Task EmailAccount_Update_PersistsDisplayNameChange()
    {
        var svc = new EmailAccountApplicationService(new FakeEmailAccountRepository());
        var id = await svc.InsertEmailAccountAsync(new EmailAccount { DisplayName = "Old", Username = "u@e.com" });
        var inserted = (await svc.GetEmailAccountsAsync())[0];
        inserted.DisplayName = "New";
        await svc.UpdateEmailAccountAsync(inserted);
        Assert.Equal("New", (await svc.GetEmailAccountsAsync())[0].DisplayName);
    }

    [Fact]
    public async Task EmailAccount_Delete_RemovesAccount()
    {
        var svc = new EmailAccountApplicationService(new FakeEmailAccountRepository());
        var id = await svc.InsertEmailAccountAsync(new EmailAccount { DisplayName = "Temp", Username = "t@e.com" });
        await svc.DeleteEmailAccountAsync(id);
        Assert.Empty(await svc.GetEmailAccountsAsync());
    }
}
