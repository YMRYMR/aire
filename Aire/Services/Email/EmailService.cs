using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using MailKit.Search;
using MailKit.Security;
using MimeKit;

namespace Aire.Services.Email
{
    /// <summary>
    /// Provides IMAP/SMTP email operations for one configured account, including OAuth refresh-token support.
    /// </summary>
    public class EmailService
    {
        private readonly EmailAccount _account;

        /// <summary>
        /// Creates the service for one stored email account configuration.
        /// </summary>
        /// <param name="account">Email account settings and credentials.</param>
        public EmailService(EmailAccount account) => _account = account;

        /// <summary>
        /// Resolves the account password in plaintext for non-OAuth authentication flows.
        /// </summary>
        private string Password => _account.PlaintextPassword
            ?? Aire.Services.SecureStorage.Unprotect(_account.EncryptedPassword)
            ?? string.Empty;

        /// <summary>
        /// Connects and authenticates an IMAP client, retrying without certificate revocation checks if the strict path fails.
        /// </summary>
        /// <param name="ct">Cancellation token for the network operation.</param>
        /// <param name="access">Optional mailbox access mode to open on the inbox after connecting.</param>
        /// <returns>A connected and authenticated IMAP client.</returns>
        private async Task<ImapClient> ConnectImapClientAsync(CancellationToken ct, FolderAccess? access = null)
        {
            ImapClient? client = null;
            try
            {
                client = new ImapClient { CheckCertificateRevocation = true };
                await client.ConnectAsync(_account.ImapHost, _account.ImapPort, SecureSocketOptions.SslOnConnect, ct);
                await AuthenticateImapAsync(client, ct);
                if (access.HasValue)
                    await client.Inbox.OpenAsync(access.Value, ct);
                return client;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception firstEx)
            {
                try { client?.Dispose(); } catch { }
                AppLogger.Warn(
                    $"{nameof(EmailService)}.{nameof(ConnectImapClientAsync)}",
                    "IMAP connect/auth failed with certificate revocation enabled, retrying without revocation check.",
                    firstEx);
                client = new ImapClient { CheckCertificateRevocation = false };
                await client.ConnectAsync(_account.ImapHost, _account.ImapPort, SecureSocketOptions.SslOnConnect, ct);
                await AuthenticateImapAsync(client, ct);
                if (access.HasValue)
                    await client.Inbox.OpenAsync(access.Value, ct);
                return client;
            }
        }

        /// <summary>
        /// Connects and authenticates an SMTP client, retrying without certificate revocation checks if the strict path fails.
        /// </summary>
        /// <param name="ct">Cancellation token for the network operation.</param>
        /// <returns>A connected and authenticated SMTP client.</returns>
        private async Task<SmtpClient> ConnectSmtpClientAsync(CancellationToken ct)
        {
            SmtpClient? client = null;
            try
            {
                client = new SmtpClient { CheckCertificateRevocation = true };
                await client.ConnectAsync(_account.SmtpHost, _account.SmtpPort, SecureSocketOptions.StartTls, ct);
                await AuthenticateSmtpAsync(client, ct);
                return client;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception firstEx)
            {
                try { client?.Dispose(); } catch { }
                AppLogger.Warn(
                    $"{nameof(EmailService)}.{nameof(ConnectSmtpClientAsync)}",
                    "SMTP connect/auth failed with certificate revocation enabled, retrying without revocation check.",
                    firstEx);
                client = new SmtpClient { CheckCertificateRevocation = false };
                await client.ConnectAsync(_account.SmtpHost, _account.SmtpPort, SecureSocketOptions.StartTls, ct);
                await AuthenticateSmtpAsync(client, ct);
                return client;
            }
        }

        /// <summary>
        /// Authenticates IMAP using either username/password or a refreshed Google OAuth access token.
        /// </summary>
        private async Task AuthenticateImapAsync(MailKit.Net.Imap.ImapClient client, CancellationToken ct)
        {
            if (_account.UseOAuth)
            {
                var refreshToken = Aire.Services.SecureStorage.Unprotect(_account.OAuthRefreshToken) ?? _account.OAuthRefreshToken;
                var accessToken  = await GoogleOAuthService.RefreshAccessTokenAsync(refreshToken, ct);
                await client.AuthenticateAsync(new MailKit.Security.SaslMechanismOAuth2(_account.Username, accessToken), ct);
            }
            else
            {
                await client.AuthenticateAsync(_account.Username, Password, ct);
            }
        }

        /// <summary>
        /// Authenticates SMTP using either username/password or a refreshed Google OAuth access token.
        /// </summary>
        private async Task AuthenticateSmtpAsync(MailKit.Net.Smtp.SmtpClient smtp, CancellationToken ct)
        {
            if (_account.UseOAuth)
            {
                var refreshToken = Aire.Services.SecureStorage.Unprotect(_account.OAuthRefreshToken) ?? _account.OAuthRefreshToken;
                var accessToken  = await GoogleOAuthService.RefreshAccessTokenAsync(refreshToken, ct);
                await smtp.AuthenticateAsync(new MailKit.Security.SaslMechanismOAuth2(_account.Username, accessToken), ct);
            }
            else
            {
                await smtp.AuthenticateAsync(_account.Username, Password, ct);
            }
        }

        /// <summary>
        /// Reads the most recent emails from the account inbox.
        /// </summary>
        /// <param name="count">Maximum number of recent messages to return.</param>
        /// <param name="ct">Cancellation token for the IMAP operations.</param>
        /// <returns>Recent email summaries ordered from newest to oldest.</returns>
        public async Task<List<EmailSummary>> FetchRecentAsync(int count = 20, CancellationToken ct = default)
        {
            using var client = await ConnectImapClientAsync(ct, FolderAccess.ReadOnly);
            var inbox = client.Inbox;

            var uids = await inbox.SearchAsync(SearchQuery.All, ct);
            var recent = uids.Reverse().Take(count).ToList();

            // Fetch flags for all recent UIDs in one batch
            var summaryItems = await inbox.FetchAsync(recent, MessageSummaryItems.UniqueId | MessageSummaryItems.Flags, ct);
            var flagsLookup = summaryItems.ToDictionary(s => s.UniqueId, s => s.Flags ?? MessageFlags.None);

            var summaries = new List<EmailSummary>();
            foreach (var uid in recent)
            {
                var msg = await inbox.GetMessageAsync(uid, ct);
                var bodyText = msg.TextBody ?? msg.HtmlBody ?? string.Empty;
                if (msg.HtmlBody != null && msg.TextBody == null)
                    bodyText = System.Text.RegularExpressions.Regex.Replace(bodyText, "<[^>]+>", " ");

                flagsLookup.TryGetValue(uid, out var flags);
                summaries.Add(new EmailSummary
                {
                    Id          = uid.ToString(),
                    Subject     = msg.Subject ?? "(no subject)",
                    From        = msg.From.ToString(),
                    Date        = msg.Date.LocalDateTime.ToString("g"),
                    BodyPreview = bodyText.Length > 200 ? bodyText[..200] + "\u2026" : bodyText,
                    IsRead      = (flags & MessageFlags.Seen) != 0,
                });
            }

            await client.DisconnectAsync(true, ct);
            return summaries;
        }

        /// <summary>
        /// Searches the inbox by subject/body keyword.
        /// </summary>
        /// <param name="query">Keyword or phrase to search for.</param>
        /// <param name="ct">Cancellation token for the IMAP operations.</param>
        /// <returns>Matching email summaries ordered from newest to oldest.</returns>
        public async Task<List<EmailSummary>> SearchAsync(string query, CancellationToken ct = default)
        {
            using var client = await ConnectImapClientAsync(ct, FolderAccess.ReadOnly);
            var inbox = client.Inbox;

            var searchQuery = SearchQuery.Or(
                SearchQuery.SubjectContains(query),
                SearchQuery.BodyContains(query));
            var uids = await inbox.SearchAsync(searchQuery, ct);

            var summaries = new List<EmailSummary>();
            foreach (var uid in uids.Reverse().Take(30))
            {
                var msg = await inbox.GetMessageAsync(uid, ct);
                var bodyText = msg.TextBody ?? string.Empty;
                summaries.Add(new EmailSummary
                {
                    Id          = uid.ToString(),
                    Subject     = msg.Subject ?? "(no subject)",
                    From        = msg.From.ToString(),
                    Date        = msg.Date.LocalDateTime.ToString("g"),
                    BodyPreview = bodyText.Length > 200 ? bodyText[..200] + "\u2026" : bodyText,
                });
            }

            await client.DisconnectAsync(true, ct);
            return summaries;
        }

        /// <summary>
        /// Sends a plain-text email through the configured SMTP account.
        /// </summary>
        /// <param name="to">Recipient email address.</param>
        /// <param name="subject">Message subject.</param>
        /// <param name="body">Plain-text message body.</param>
        /// <param name="ct">Cancellation token for the SMTP operations.</param>
        public async Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
        {
            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(_account.Username));
            message.To.Add(MailboxAddress.Parse(to));
            message.Subject = subject;
            message.Body    = new TextPart("plain") { Text = body };

            using var smtp = await ConnectSmtpClientAsync(ct);
            await smtp.SendAsync(message, ct);
            await smtp.DisconnectAsync(true, ct);
        }

        /// <summary>
        /// Replies to an existing message by IMAP UID and preserves threading headers when possible.
        /// </summary>
        /// <param name="originalUidStr">Original message UID returned by read/search operations.</param>
        /// <param name="body">Plain-text reply body.</param>
        /// <param name="ct">Cancellation token for the IMAP/SMTP operations.</param>
        public async Task ReplyAsync(string originalUidStr, string body, CancellationToken ct = default)
        {
            using var imap = await ConnectImapClientAsync(ct, FolderAccess.ReadOnly);
            var inbox = imap.Inbox;

            MailKit.UniqueId.TryParse(originalUidStr, out var uid);
            var original = await inbox.GetMessageAsync(uid, ct);
            await imap.DisconnectAsync(true, ct);

            // Build reply manually
            var reply = new MimeMessage();
            reply.From.Add(new MailboxAddress(_account.DisplayName, _account.Username));

            // Reply-To takes precedence over From for the recipient
            if (original.ReplyTo.Count > 0)
                reply.To.AddRange(original.ReplyTo);
            else
                reply.To.AddRange(original.From);

            reply.Subject = original.Subject?.StartsWith("Re:", StringComparison.OrdinalIgnoreCase) == true
                ? original.Subject
                : $"Re: {original.Subject}";

            // Threading headers
            if (!string.IsNullOrEmpty(original.MessageId))
            {
                reply.InReplyTo = original.MessageId;
                reply.References.AddRange(original.References);
                reply.References.Add(original.MessageId);
            }

            reply.Body = new TextPart("plain") { Text = body };

            using var smtp = await ConnectSmtpClientAsync(ct);
            await smtp.SendAsync(reply, ct);
            await smtp.DisconnectAsync(true, ct);
        }

        /// <summary>
        /// Verifies that the account can connect and authenticate to IMAP.
        /// </summary>
        /// <param name="ct">Cancellation token for the connection test.</param>
        /// <returns>A success flag and the thrown error message when the connection test fails.</returns>
        public async Task<(bool Ok, string? Error)> TestConnectionAsync(CancellationToken ct = default)
        {
            try
            {
                using var client = await ConnectImapClientAsync(ct);
                await client.DisconnectAsync(true, ct);
                return (true, null);
            }
            catch
            {
                return (false, "Email connection failed.");
            }
        }
    }
}
