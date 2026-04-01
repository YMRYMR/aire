using Aire.Services.Email;
using Xunit;

namespace Aire.Tests.Services;

public class EmailAccountCoverageTests
{
    [Fact]
    public void GmailPreset_PopulatesExpectedDefaults()
    {
        EmailAccount emailAccount = EmailAccount.GmailPreset("Personal", "user@example.com");
        Assert.Equal("Personal", emailAccount.DisplayName);
        Assert.Equal(EmailProvider.Gmail, emailAccount.Provider);
        Assert.Equal("imap.gmail.com", emailAccount.ImapHost);
        Assert.Equal(993, emailAccount.ImapPort);
        Assert.Equal("smtp.gmail.com", emailAccount.SmtpHost);
        Assert.Equal(587, emailAccount.SmtpPort);
        Assert.Equal("user@example.com", emailAccount.Username);
        Assert.True(emailAccount.IsEnabled);
    }

    [Fact]
    public void OutlookPreset_PopulatesExpectedDefaults()
    {
        EmailAccount emailAccount = EmailAccount.OutlookPreset("Work", "user@example.com");
        Assert.Equal("Work", emailAccount.DisplayName);
        Assert.Equal(EmailProvider.Outlook, emailAccount.Provider);
        Assert.Equal("outlook.office365.com", emailAccount.ImapHost);
        Assert.Equal(993, emailAccount.ImapPort);
        Assert.Equal("smtp.office365.com", emailAccount.SmtpHost);
        Assert.Equal(587, emailAccount.SmtpPort);
        Assert.Equal("user@example.com", emailAccount.Username);
        Assert.True(emailAccount.IsEnabled);
    }

    [Fact]
    public void NewEmailAccount_HasSafeDefaults()
    {
        EmailAccount emailAccount = new EmailAccount();
        Assert.Equal(0, emailAccount.Id);
        Assert.Equal(string.Empty, emailAccount.DisplayName);
        Assert.Equal(EmailProvider.Gmail, emailAccount.Provider);
        Assert.Equal(string.Empty, emailAccount.ImapHost);
        Assert.Equal(993, emailAccount.ImapPort);
        Assert.Equal(string.Empty, emailAccount.SmtpHost);
        Assert.Equal(587, emailAccount.SmtpPort);
        Assert.Equal(string.Empty, emailAccount.Username);
        Assert.Equal(string.Empty, emailAccount.EncryptedPassword);
        Assert.Equal(string.Empty, emailAccount.OAuthRefreshToken);
        Assert.True(emailAccount.IsEnabled);
        Assert.Null(emailAccount.PlaintextPassword);
        Assert.False(emailAccount.UseOAuth);
    }
}
