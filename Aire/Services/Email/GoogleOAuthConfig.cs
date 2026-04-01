namespace Aire.Services.Email
{
    /// <summary>
    /// Google OAuth2 credentials for the Gmail IMAP/SMTP flow.
    ///
    /// Set the following environment variables before running Aire with Gmail support:
    ///   AIRE_GOOGLE_CLIENT_ID     — OAuth 2.0 Client ID (ends in .apps.googleusercontent.com)
    ///   AIRE_GOOGLE_CLIENT_SECRET — OAuth 2.0 Client Secret
    ///
    /// To obtain credentials:
    ///   1. Go to https://console.cloud.google.com/ → APIs &amp; Services → Credentials
    ///   2. Create OAuth 2.0 Client ID → Application type: Desktop app
    ///   3. Enable the Gmail API under APIs &amp; Services → Enabled APIs
    /// </summary>
    internal static class GoogleOAuthConfig
    {
        public static string ClientId =>
            Environment.GetEnvironmentVariable("AIRE_GOOGLE_CLIENT_ID") ?? string.Empty;

        public static string ClientSecret =>
            Environment.GetEnvironmentVariable("AIRE_GOOGLE_CLIENT_SECRET") ?? string.Empty;
    }
}
