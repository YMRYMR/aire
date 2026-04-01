using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Aire.Services;
using Aire.Services.Email;
using Xunit;

namespace Aire.Tests.Services;

public class EmailServiceCoverageTests
{
    [Fact]
    public void PasswordProperty_PrefersPlaintext_AndDecryptsStoredValue()
    {
        string encryptedPassword = SecureStorage.Protect("secret-password");
        EmailAccount account = new EmailAccount
        {
            Username = "user@example.com",
            PlaintextPassword = "plain",
            EncryptedPassword = encryptedPassword
        };
        EmailAccount account2 = new EmailAccount
        {
            Username = "user@example.com",
            EncryptedPassword = encryptedPassword
        };
        EmailService obj = new EmailService(account);
        EmailService obj2 = new EmailService(account2);
        PropertyInfo property = typeof(EmailService).GetProperty("Password", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.Equal("plain", property.GetValue(obj));
        Assert.Equal("secret-password", property.GetValue(obj2));
    }

    [Fact]
    public async Task TestConnectionAsync_CancelledToken_ReturnsFailureTuple()
    {
        EmailService service = new EmailService(new EmailAccount
        {
            Username = "user@example.com",
            EncryptedPassword = SecureStorage.Protect("pw"),
            ImapHost = "127.0.0.1",
            ImapPort = 65535,
            SmtpHost = "127.0.0.1",
            SmtpPort = 65535
        });
        using CancellationTokenSource cts = new CancellationTokenSource();
        cts.Cancel();
        var (ok, error) = await service.TestConnectionAsync(cts.Token);
        Assert.False(ok);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }
}
