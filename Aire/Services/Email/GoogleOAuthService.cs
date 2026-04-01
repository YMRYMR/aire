using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Aire.Services.Email
{
    /// <summary>
    /// Result returned by the Google OAuth PKCE flow.
    /// </summary>
    public class OAuthTokenResult
    {
        public string   AccessToken  { get; init; } = string.Empty;
        public string   RefreshToken { get; init; } = string.Empty;
        public DateTime ExpiresAt    { get; init; }
    }

    /// <summary>
    /// Handles the Google OAuth browser flow used for Gmail accounts.
    /// </summary>
    public static class GoogleOAuthService
    {
        private const string AuthEndpoint  = "https://accounts.google.com/o/oauth2/v2/auth";
        private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
        private const string Scope         = "https://mail.google.com/";

        /// <summary>
        /// Runs the browser-based OAuth2 PKCE flow and returns access + refresh tokens.
        /// </summary>
        /// <param name="ct">Cancellation token used to abort the browser callback wait.</param>
        /// <returns>OAuth tokens and their expiration metadata.</returns>
        public static async Task<OAuthTokenResult> AuthorizeAsync(CancellationToken ct = default)
        {
            var port         = GetFreePort();
            var redirectUri  = $"http://localhost:{port}/";
            var state        = Guid.NewGuid().ToString("N");
            var codeVerifier = GenerateCodeVerifier();
            var challenge    = GenerateCodeChallenge(codeVerifier);

            var authUrl = AuthEndpoint
                + $"?client_id={Uri.EscapeDataString(GoogleOAuthConfig.ClientId)}"
                + $"&redirect_uri={Uri.EscapeDataString(redirectUri)}"
                + $"&response_type=code"
                + $"&scope={Uri.EscapeDataString(Scope)}"
                + $"&state={state}"
                + $"&code_challenge={challenge}"
                + $"&code_challenge_method=S256"
                + $"&access_type=offline"
                + $"&prompt=consent";

            using var listener = new HttpListener();
            listener.Prefixes.Add(redirectUri);
            listener.Start();

            Process.Start(new ProcessStartInfo(authUrl) { UseShellExecute = true });

            // Wait for the redirect callback
            var contextTask = listener.GetContextAsync();
            var cancelTcs   = new TaskCompletionSource<HttpListenerContext>();
            using var reg   = ct.Register(() => cancelTcs.TrySetCanceled());

            var winner = await Task.WhenAny(contextTask, cancelTcs.Task);
            if (winner != contextTask) throw new OperationCanceledException(ct);

            var context = await contextTask;

            // Send a close-tab page back to the browser
            var html  = "<html><body style='font-family:sans-serif;padding:2em'><h2>Done!</h2><p>You can close this tab and return to Aire.</p></body></html>";
            var bytes = Encoding.UTF8.GetBytes(html);
            context.Response.ContentType     = "text/html";
            context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes, ct);
            context.Response.Close();
            listener.Stop();

            var query = context.Request.QueryString;
            var code  = query["code"]  ?? throw new InvalidOperationException("Google did not return an authorization code.");
            if (query["state"] != state) throw new InvalidOperationException("OAuth state mismatch — possible CSRF.");

            return await ExchangeCodeAsync(code, redirectUri, codeVerifier, ct);
        }

        /// <summary>
        /// Uses a stored refresh token to get a fresh access token.
        /// </summary>
        /// <param name="refreshToken">Previously issued Google refresh token.</param>
        /// <param name="ct">Cancellation token for the token refresh request.</param>
        /// <returns>A fresh short-lived access token.</returns>
        public static async Task<string> RefreshAccessTokenAsync(string refreshToken, CancellationToken ct = default)
        {
            using var http = new HttpClient();
            var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"]     = GoogleOAuthConfig.ClientId,
                ["client_secret"] = GoogleOAuthConfig.ClientSecret,
                ["refresh_token"] = refreshToken,
                ["grant_type"]    = "refresh_token",
            });
            var resp = await http.PostAsync(TokenEndpoint, form, ct);
            resp.EnsureSuccessStatusCode();
            var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            return doc.RootElement.GetProperty("access_token").GetString()
                ?? throw new InvalidOperationException("No access_token in refresh response.");
        }

        /// <summary>
        /// Exchanges the OAuth authorization code for access and refresh tokens.
        /// </summary>
        private static async Task<OAuthTokenResult> ExchangeCodeAsync(
            string code, string redirectUri, string codeVerifier, CancellationToken ct)
        {
            using var http = new HttpClient();
            var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"]          = code,
                ["client_id"]     = GoogleOAuthConfig.ClientId,
                ["client_secret"] = GoogleOAuthConfig.ClientSecret,
                ["redirect_uri"]  = redirectUri,
                ["grant_type"]    = "authorization_code",
                ["code_verifier"] = codeVerifier,
            });
            var resp = await http.PostAsync(TokenEndpoint, form, ct);
            resp.EnsureSuccessStatusCode();
            var root      = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct)).RootElement;
            var expiresIn = root.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 3600;
            return new OAuthTokenResult
            {
                AccessToken  = root.GetProperty("access_token").GetString() ?? "",
                RefreshToken = root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() ?? "" : "",
                ExpiresAt    = DateTime.UtcNow.AddSeconds(expiresIn - 60),
            };
        }

        /// <summary>
        /// Reserves a temporary loopback port for the local OAuth callback listener.
        /// </summary>
        internal static int GetFreePort()
        {
            var l = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            l.Start();
            var port = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return port;
        }

        /// <summary>
        /// Creates a high-entropy PKCE code verifier.
        /// </summary>
        internal static string GenerateCodeVerifier()
        {
            var bytes = new byte[32];
            RandomNumberGenerator.Fill(bytes);
            return Base64UrlEncode(bytes);
        }

        /// <summary>
        /// Derives the PKCE code challenge from a verifier using SHA-256 and base64url encoding.
        /// </summary>
        internal static string GenerateCodeChallenge(string verifier)
        {
            var hash = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
            return Base64UrlEncode(hash);
        }

        /// <summary>
        /// Encodes binary data using the URL-safe base64 alphabet required by PKCE.
        /// </summary>
        internal static string Base64UrlEncode(byte[] bytes)
            => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
