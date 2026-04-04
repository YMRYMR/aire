using System;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Application = System.Windows.Application;

namespace Aire.Providers
{
    /// <summary>
    /// Manages a permanent hidden WebView2 browser that stays alive for the
    /// entire app session. API calls are executed as JavaScript inside it so
    /// cookies and fingerprinting are handled natively by the browser.
    /// </summary>
    internal sealed class ClaudeAiSession
    {
        public static readonly ClaudeAiSession Instance = new();

        private Window? _host;
        private WebView2? _view;
        private Channel<string>? _channel;
        private readonly SemaphoreSlim _lock = new(1, 1);

        private ClaudeAiSession() { }

        public bool IsReady { get; internal set; }

        /// <summary>
        /// Set by App.xaml.cs at startup. When invoked, shows the Claude.ai login window
        /// and waits for the user to complete (or cancel) login.
        /// </summary>
        public static Func<Task>? PromptLogin { get; set; }

        /// <summary>
        /// Called after login. Copies all claude.ai cookies from the login
        /// browser into a new permanent hidden WebView2 that we own, then
        /// navigates it to claude.ai to establish the right fetch origin.
        /// The login window can be safely disposed after this returns.
        /// </summary>
        internal async Task AttachAsync(WebView2 loginView)
        {
            IsReady = false;

            var cookies = await loginView.CoreWebView2.CookieManager
                .GetCookiesAsync("https://claude.ai");

            // Create our own hidden host window the first time
            if (_host == null)
            {
                _host = new Window
                {
                    Width = 800, Height = 600,
                    WindowStyle = WindowStyle.None,
                    ShowInTaskbar = false,
                    Title = "Aire — Claude Session"
                };
                _view = new WebView2();
                _host.Content = _view;
                _host.Show(); // must be shown for WebView2 to initialize
                await _view.EnsureCoreWebView2Async(null);
                _view.CoreWebView2.WebMessageReceived += OnMessage;
                _host.Hide();
            }
            else
            {
                _view!.CoreWebView2.CookieManager.DeleteAllCookies();
            }

            // Copy every cookie from the login browser
            foreach (var c in cookies)
                _view!.CoreWebView2.CookieManager.AddOrUpdateCookie(c);

            // Navigate to claude.ai so fetch() calls run in the right origin
            var navDone = new TaskCompletionSource();
            void NavHandler(object? s, CoreWebView2NavigationCompletedEventArgs e)
            {
                _view!.CoreWebView2.NavigationCompleted -= NavHandler;
                navDone.TrySetResult();
            }
            _view!.CoreWebView2.NavigationCompleted += NavHandler;
            _view.CoreWebView2.Navigate("https://claude.ai");
            await navDone.Task;

            IsReady = true;
        }

        private void OnMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            var msg = e.TryGetWebMessageAsString();
            if (msg != null) _channel?.Writer.TryWrite(msg);
        }

        // ── One-shot request ──────────────────────────────────────────────────

        public async Task<string> RequestAsync(string method, string url,
            string? jsonBody = null, CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            var ch = Channel.CreateUnbounded<string>();
            _channel = ch;

            var bodyJs = jsonBody != null ? $"body: {JsonSerializer.Serialize(jsonBody)}," : "";

            var js = $$"""
                (async () => {
                    try {
                        const r = await fetch({{JsonSerializer.Serialize(url)}}, {
                            method: '{{method}}',
                            headers: { 'Content-Type': 'application/json', 'Accept': 'application/json' },
                            {{bodyJs}}
                        });
                        const t = await r.text();
                        if (!r.ok) { window.chrome.webview.postMessage('[ERR]Claude session request failed.'); return; }
                        window.chrome.webview.postMessage(t);
                    } catch(e) {
                        window.chrome.webview.postMessage('[ERR]Claude session request failed.');
                    }
                })(); null
                """;

            _ = Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                try { await _view!.CoreWebView2.ExecuteScriptAsync(js); }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Claude session script execution failed: {ex.GetType().Name}");
                    ch.Writer.TryWrite("[ERR]Claude session request failed.");
                }
            });

            try
            {
                var msg = await ch.Reader.ReadAsync(ct);
                if (msg.StartsWith("[ERR]")) throw new Exception("Claude session request failed.");
                return msg;
            }
            finally
            {
                _channel = null;
                _lock.Release();
            }
        }

        // ── Streaming SSE request ─────────────────────────────────────────────

        public async IAsyncEnumerable<string> StreamAsync(string url, string jsonBody,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct);
            var ch = Channel.CreateUnbounded<string>();
            _channel = ch;

            var js = $$"""
                (async () => {
                    try {
                        const r = await fetch({{JsonSerializer.Serialize(url)}}, {
                            method: 'POST',
                            headers: { 'Content-Type': 'application/json', 'Accept': 'text/event-stream' },
                            body: {{JsonSerializer.Serialize(jsonBody)}}
                        });
                        if (!r.ok) {
                            const t = await r.text();
                            window.chrome.webview.postMessage('[ERR]Claude session streaming failed.');
                            return;
                        }
                        const reader = r.body.getReader();
                        const decoder = new TextDecoder();
                        let buf = '';
                        while (true) {
                            const { done, value } = await reader.read();
                            if (done) break;
                            buf += decoder.decode(value, { stream: true });
                            const lines = buf.split('\n');
                            buf = lines.pop();
                            for (const line of lines) {
                                if (!line.startsWith('data: ')) continue;
                                const data = line.slice(6).trim();
                                if (data === '[DONE]') { window.chrome.webview.postMessage('[DONE]'); return; }
                                try {
                                    const obj = JSON.parse(data);
                                    const chunk = obj.completion
                                        ?? (obj.delta && obj.delta.text)
                                        ?? '';
                                    if (chunk) window.chrome.webview.postMessage(chunk);
                                } catch(_) {}
                            }
                        }
                        window.chrome.webview.postMessage('[DONE]');
                    } catch(e) {
                        window.chrome.webview.postMessage('[ERR]Claude session streaming failed.');
                    }
                })(); null
                """;

            _ = Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                try { await _view!.CoreWebView2.ExecuteScriptAsync(js); }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Claude session streaming script failed: {ex.GetType().Name}");
                    ch.Writer.TryWrite("[ERR]Claude session streaming failed.");
                }
            });

            try
            {
                await foreach (var msg in ch.Reader.ReadAllAsync(ct))
                {
                    if (msg == "[DONE]") break;
                    if (msg.StartsWith("[ERR]")) throw new Exception("Claude session streaming failed.");
                    yield return msg;
                }
            }
            finally
            {
                _channel = null;
                _lock.Release();
            }
        }
    }
}
