using Aire.AppLayer.Abstractions;
using Aire.AppLayer.Connections;
using Aire.Services.Email;
using Xunit;

namespace Aire.Tests.Services;

public sealed class EmailAccountApplicationServiceTests
{
    /// <summary>
    /// In-memory fake that records calls so tests can verify delegation.
    /// </summary>
    private sealed class FakeEmailAccountRepository : IEmailAccountRepository
    {
        private readonly List<EmailAccount> _accounts = [];
        private int _nextId = 1;

        public int LastDeletedId { get; private set; }
        public EmailAccount? LastUpdated { get; private set; }

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
            LastUpdated = account;
            return Task.CompletedTask;
        }

        public Task DeleteEmailAccountAsync(int id)
        {
            _accounts.RemoveAll(a => a.Id == id);
            LastDeletedId = id;
            return Task.CompletedTask;
        }
    }

    // ── GetEmailAccountsAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task GetEmailAccountsAsync_ReturnsEmptyWhenNoneConfigured()
    {
        var svc = new EmailAccountApplicationService(new FakeEmailAccountRepository());

        var result = await svc.GetEmailAccountsAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetEmailAccountsAsync_ReturnsAccountsFromRepository()
    {
        var repo = new FakeEmailAccountRepository();
        var svc = new EmailAccountApplicationService(repo);

        await svc.InsertEmailAccountAsync(new EmailAccount { DisplayName = "Work", Username = "me@work.com" });
        await svc.InsertEmailAccountAsync(new EmailAccount { DisplayName = "Personal", Username = "me@home.com" });

        var result = await svc.GetEmailAccountsAsync();

        Assert.Equal(2, result.Count);
        Assert.Equal("Work", result[0].DisplayName);
        Assert.Equal("Personal", result[1].DisplayName);
    }

    // ── InsertEmailAccountAsync ────────────────────────────────────────────────

    [Fact]
    public async Task InsertEmailAccountAsync_DelegatesAndReturnsGeneratedId()
    {
        var repo = new FakeEmailAccountRepository();
        var svc = new EmailAccountApplicationService(repo);

        var account = new EmailAccount { DisplayName = "Gmail", Username = "user@gmail.com" };
        var id = await svc.InsertEmailAccountAsync(account);

        Assert.True(id > 0);
        var list = await svc.GetEmailAccountsAsync();
        Assert.Single(list);
        Assert.Equal("Gmail", list[0].DisplayName);
        Assert.Equal(id, list[0].Id);
    }

    [Fact]
    public async Task InsertEmailAccountAsync_AssignsSequentialIds()
    {
        var svc = new EmailAccountApplicationService(new FakeEmailAccountRepository());

        var id1 = await svc.InsertEmailAccountAsync(new EmailAccount { DisplayName = "A" });
        var id2 = await svc.InsertEmailAccountAsync(new EmailAccount { DisplayName = "B" });

        Assert.True(id2 > id1);
    }

    // ── UpdateEmailAccountAsync ────────────────────────────────────────────────

    [Fact]
    public async Task UpdateEmailAccountAsync_DelegatesWithCorrectAccount()
    {
        var repo = new FakeEmailAccountRepository();
        var svc = new EmailAccountApplicationService(repo);

        await svc.InsertEmailAccountAsync(new EmailAccount { DisplayName = "Old", Username = "u@e.com" });
        var inserted = (await svc.GetEmailAccountsAsync())[0];
        inserted.DisplayName = "New";

        await svc.UpdateEmailAccountAsync(inserted);

        Assert.Equal("New", repo.LastUpdated!.DisplayName);
        Assert.Equal(inserted.Id, repo.LastUpdated.Id);
    }

    [Fact]
    public async Task UpdateEmailAccountAsync_PersistsChanges()
    {
        var svc = new EmailAccountApplicationService(new FakeEmailAccountRepository());

        await svc.InsertEmailAccountAsync(new EmailAccount { DisplayName = "Old", Username = "u@e.com" });
        var inserted = (await svc.GetEmailAccountsAsync())[0];
        inserted.DisplayName = "New";

        await svc.UpdateEmailAccountAsync(inserted);

        var updated = (await svc.GetEmailAccountsAsync())[0];
        Assert.Equal("New", updated.DisplayName);
    }

    // ── DeleteEmailAccountAsync ────────────────────────────────────────────────

    [Fact]
    public async Task DeleteEmailAccountAsync_DelegatesWithCorrectId()
    {
        var repo = new FakeEmailAccountRepository();
        var svc = new EmailAccountApplicationService(repo);

        var id = await svc.InsertEmailAccountAsync(new EmailAccount { DisplayName = "Temp", Username = "t@e.com" });

        await svc.DeleteEmailAccountAsync(id);

        Assert.Equal(id, repo.LastDeletedId);
    }

    [Fact]
    public async Task DeleteEmailAccountAsync_RemovesAccountFromList()
    {
        var svc = new EmailAccountApplicationService(new FakeEmailAccountRepository());

        var id = await svc.InsertEmailAccountAsync(new EmailAccount { DisplayName = "Temp", Username = "t@e.com" });
        await svc.InsertEmailAccountAsync(new EmailAccount { DisplayName = "Keep", Username = "k@e.com" });

        await svc.DeleteEmailAccountAsync(id);

        var remaining = await svc.GetEmailAccountsAsync();
        Assert.Single(remaining);
        Assert.Equal("Keep", remaining[0].DisplayName);
    }

    // ── GmailPreset / OutlookPreset integration ───────────────────────────────

    [Fact]
    public async Task InsertEmailAccountAsync_GmailPreset_SetsCorrectHosts()
    {
        var svc = new EmailAccountApplicationService(new FakeEmailAccountRepository());

        var account = EmailAccount.GmailPreset("My Gmail", "user@gmail.com");
        await svc.InsertEmailAccountAsync(account);

        var stored = (await svc.GetEmailAccountsAsync())[0];
        Assert.Equal("imap.gmail.com", stored.ImapHost);
        Assert.Equal("smtp.gmail.com", stored.SmtpHost);
        Assert.Equal(EmailProvider.Gmail, stored.Provider);
    }

    [Fact]
    public async Task InsertEmailAccountAsync_OutlookPreset_SetsCorrectHosts()
    {
        var svc = new EmailAccountApplicationService(new FakeEmailAccountRepository());

        var account = EmailAccount.OutlookPreset("My Outlook", "user@outlook.com");
        await svc.InsertEmailAccountAsync(account);

        var stored = (await svc.GetEmailAccountsAsync())[0];
        Assert.Equal("outlook.office365.com", stored.ImapHost);
        Assert.Equal("smtp.office365.com", stored.SmtpHost);
        Assert.Equal(EmailProvider.Outlook, stored.Provider);
    }
}
