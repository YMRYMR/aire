using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Aire.Data;
using Aire.Services.Email;
using static Aire.Services.Tools.ToolHelpers;

namespace Aire.Services.Tools
{
    public class EmailToolService
    {
        private readonly DatabaseService _db;

        public EmailToolService(DatabaseService db) => _db = db;

        public bool IsConfigured { get; private set; }

        public async Task RefreshIsConfiguredAsync()
        {
            var accounts = await _db.GetEmailAccountsAsync();
            IsConfigured = accounts.Any(a => a.IsEnabled);
        }

        public async Task<ToolExecutionResult> ExecuteAsync(Aire.Services.ToolCallRequest request)
        {
            try
            {
                return request.Tool switch
                {
                    "read_emails"    => await ReadEmailsAsync(request),
                    "send_email"     => await SendEmailAsync(request),
                    "search_emails"  => await SearchEmailsAsync(request),
                    "reply_to_email" => await ReplyToEmailAsync(request),
                    _ => new ToolExecutionResult { TextResult = $"Unknown email tool: {request.Tool}" }
                };
            }
            catch (Exception ex)
            {
                return new ToolExecutionResult { TextResult = $"Email error: {ex.Message}" };
            }
        }

        private async Task<EmailService> GetServiceAsync(string? accountName)
        {
            var accounts = await _db.GetEmailAccountsAsync();
            var account  = string.IsNullOrWhiteSpace(accountName)
                ? accounts.FirstOrDefault(a => a.IsEnabled)
                : accounts.FirstOrDefault(a => a.IsEnabled &&
                    a.DisplayName.Contains(accountName, StringComparison.OrdinalIgnoreCase));

            if (account == null)
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(accountName)
                        ? "No email account configured. Add one in Settings \u2192 Connections."
                        : $"No enabled email account matching '{accountName}'.");

            account.PlaintextPassword = SecureStorage.Unprotect(account.EncryptedPassword);
            return new EmailService(account);
        }

        private async Task<ToolExecutionResult> ReadEmailsAsync(Aire.Services.ToolCallRequest req)
        {
            var account = GetString(req, "account");
            var count   = GetInt(req, "count", 20);
            var svc     = await GetServiceAsync(account);
            var emails  = await svc.FetchRecentAsync(count);
            return new ToolExecutionResult { TextResult = FormatEmailList(emails) };
        }

        private async Task<ToolExecutionResult> SendEmailAsync(Aire.Services.ToolCallRequest req)
        {
            var to      = GetString(req, "to");
            var subject = GetString(req, "subject");
            var body    = GetString(req, "body");
            var account = GetString(req, "account");
            if (string.IsNullOrWhiteSpace(to))
                return new ToolExecutionResult { TextResult = "Error: 'to' parameter is required." };

            var svc = await GetServiceAsync(account);
            await svc.SendAsync(to, subject, body);
            return new ToolExecutionResult { TextResult = $"Email sent to {to}." };
        }

        private async Task<ToolExecutionResult> SearchEmailsAsync(Aire.Services.ToolCallRequest req)
        {
            var query   = GetString(req, "query");
            var account = GetString(req, "account");
            if (string.IsNullOrWhiteSpace(query))
                return new ToolExecutionResult { TextResult = "Error: 'query' parameter is required." };

            var svc    = await GetServiceAsync(account);
            var emails = await svc.SearchAsync(query);
            return new ToolExecutionResult { TextResult = FormatEmailList(emails) };
        }

        private async Task<ToolExecutionResult> ReplyToEmailAsync(Aire.Services.ToolCallRequest req)
        {
            var messageId = GetString(req, "message_id");
            var body      = GetString(req, "body");
            var account   = GetString(req, "account");
            if (string.IsNullOrWhiteSpace(messageId))
                return new ToolExecutionResult { TextResult = "Error: 'message_id' is required." };

            var svc = await GetServiceAsync(account);
            await svc.ReplyAsync(messageId, body);
            return new ToolExecutionResult { TextResult = "Reply sent." };
        }

        private static string FormatEmailList(List<EmailSummary> emails)
        {
            if (emails.Count == 0) return "No emails found.";
            var sb = new StringBuilder();
            foreach (var e in emails)
            {
                sb.AppendLine($"ID: {e.Id}");
                sb.AppendLine($"From: {e.From}");
                sb.AppendLine($"Subject: {e.Subject}");
                sb.AppendLine($"Date: {e.Date}");
                sb.AppendLine($"Preview: {e.BodyPreview}");
                sb.AppendLine();
            }
            return sb.ToString().Trim();
        }
    }
}
