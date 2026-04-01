namespace Aire.Services.Email
{
    /// <summary>
    /// Preset families supported by the email-account UI.
    /// </summary>
    public enum EmailProvider { Gmail, Outlook, Custom }

    /// <summary>
    /// Persisted email account configuration used by the email integration features.
    /// </summary>
    public class EmailAccount
    {
        public int           Id                { get; set; }
        public string        DisplayName       { get; set; } = string.Empty;
        public EmailProvider Provider          { get; set; }
        public string        ImapHost          { get; set; } = string.Empty;
        public int           ImapPort          { get; set; } = 993;
        public string        SmtpHost          { get; set; } = string.Empty;
        public int           SmtpPort          { get; set; } = 587;
        public string        Username          { get; set; } = string.Empty;
        public string        EncryptedPassword    { get; set; } = string.Empty;  // dpapi: prefix
        public bool          UseOAuth             { get; set; }
        public string        OAuthRefreshToken    { get; set; } = string.Empty;  // dpapi: prefix
        public bool          IsEnabled            { get; set; } = true;

        // In-memory only — never persisted
        public string? PlaintextPassword { get; set; }

        /// <summary>
        /// Creates a Gmail account prefilled with the standard IMAP/SMTP endpoints.
        /// </summary>
        /// <param name="displayName">User-visible name for the account.</param>
        /// <param name="username">Email address or login name.</param>
        /// <returns>A Gmail-flavored account preset.</returns>
        public static EmailAccount GmailPreset(string displayName, string username) => new()
        {
            DisplayName = displayName,
            Provider    = EmailProvider.Gmail,
            ImapHost    = "imap.gmail.com",
            ImapPort    = 993,
            SmtpHost    = "smtp.gmail.com",
            SmtpPort    = 587,
            Username    = username,
        };

        /// <summary>
        /// Creates an Outlook account prefilled with the standard IMAP/SMTP endpoints.
        /// </summary>
        /// <param name="displayName">User-visible name for the account.</param>
        /// <param name="username">Email address or login name.</param>
        /// <returns>An Outlook-flavored account preset.</returns>
        public static EmailAccount OutlookPreset(string displayName, string username) => new()
        {
            DisplayName = displayName,
            Provider    = EmailProvider.Outlook,
            ImapHost    = "outlook.office365.com",
            ImapPort    = 993,
            SmtpHost    = "smtp.office365.com",
            SmtpPort    = 587,
            Username    = username,
        };
    }
}
