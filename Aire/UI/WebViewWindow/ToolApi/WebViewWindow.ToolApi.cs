using System;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Aire.UI;

public partial class WebViewWindow
{
    /// <summary>
    /// Returns a text summary of all open browser tabs for tool and local API callers.
    /// </summary>
    public string ListTabs()
    {
        if (_tabs.Count == 0) return "No tabs open.";
        var active = ActiveTab;
        var lines = _tabs.Select((t, i) => $"Tab {i}{(t == active ? " (active)" : "")}: {t.Url}  —  {t.Title}");
        return string.Join("\n", lines);
    }

    /// <summary>
    /// Reads visible body text from one browser tab and returns it with URL/title metadata.
    /// </summary>
    /// <param name="index">Tab index to read, or -1 for the active tab.</param>
    /// <returns>Plain text content for the requested tab, or an error string.</returns>
    public async Task<string> ReadTabContentAsync(int index = -1)
    {
        Tab? tab = index < 0 ? ActiveTab : (index < _tabs.Count ? _tabs[index] : null);
        if (tab is null) return "Tab not found.";
        try
        {
            await tab.WebView.EnsureCoreWebView2Async();
            var json = await tab.WebView.CoreWebView2.ExecuteScriptAsync("document.body ? document.body.innerText : ''");
            var text = JsonSerializer.Deserialize<string>(json) ?? string.Empty;
            const int max = 25_000;
            if (text.Length > max) text = text[..max] + "\n[…content truncated…]";
            return $"URL: {tab.Url}\nTitle: {tab.Title}\n\n{text}";
        }
        catch (Exception)
        {
            return "Error reading tab.";
        }
    }

    /// <summary>
    /// Makes one tab active by index.
    /// </summary>
    /// <param name="index">Zero-based tab index.</param>
    /// <returns>A short status message describing the result.</returns>
    public string SwitchTab(int index)
    {
        if (index < 0 || index >= _tabs.Count)
            return $"Error: Tab index {index} out of range (0–{_tabs.Count - 1}).";
        BrowserTabs.SelectedItem = _tabs[index].TabItem;
        return $"Switched to tab {index}: {_tabs[index].Title}";
    }

    /// <summary>
    /// Closes one browser tab by index, defaulting to the active tab.
    /// </summary>
    /// <param name="index">Tab index to close, or -1 for the active tab.</param>
    /// <returns>A short status message describing the result.</returns>
    public string CloseTabByIndex(int index)
    {
        Tab? tab = index < 0 ? ActiveTab : (index < _tabs.Count ? _tabs[index] : null);
        if (tab is null) return "Tab not found.";
        CloseTab(tab);
        return $"Tab closed. {_tabs.Count} tab(s) remaining.";
    }

    /// <summary>
    /// Returns the full HTML document for one tab, truncated to a safe maximum size.
    /// </summary>
    /// <param name="index">Tab index to inspect, or -1 for the active tab.</param>
    /// <returns>HTML content with URL/title metadata, or an error string.</returns>
    public async Task<string> GetTabHtmlAsync(int index = -1)
    {
        Tab? tab = index < 0 ? ActiveTab : (index < _tabs.Count ? _tabs[index] : null);
        if (tab is null) return "Tab not found.";
        try
        {
            await tab.WebView.EnsureCoreWebView2Async();
            var json = await tab.WebView.CoreWebView2.ExecuteScriptAsync("document.documentElement.outerHTML");
            var html = JsonSerializer.Deserialize<string>(json) ?? string.Empty;
            const int max = 80_000;
            if (html.Length > max) html = html[..max] + "\n<!-- [truncated] -->";
            return $"URL: {tab.Url}\nTitle: {tab.Title}\n\n{html}";
        }
        catch (Exception)
        {
            return "Error reading HTML.";
        }
    }

    /// <summary>
    /// Executes arbitrary JavaScript in one tab and returns the serialized result.
    /// </summary>
    /// <param name="index">Zero-based tab index, or -1 for the active tab.</param>
    /// <param name="script">JavaScript body to execute inside an IIFE wrapper.</param>
    /// <returns>The script result or an error string.</returns>
    public async Task<string> ExecuteScriptInTabAsync(int index, string script)
    {
        Tab? tab = index < 0 ? ActiveTab : (index < _tabs.Count ? _tabs[index] : null);
        if (tab is null) return "Tab not found.";
        try
        {
            await tab.WebView.EnsureCoreWebView2Async();
            var wrapped = $"(function(){{ {script} }})()";
            var resultJson = await tab.WebView.CoreWebView2.ExecuteScriptAsync(wrapped);
            if (resultJson.StartsWith('"') && resultJson.EndsWith('"'))
            {
                try { return JsonSerializer.Deserialize<string>(resultJson) ?? resultJson; }
                catch { /* not a valid JSON string — return raw resultJson as-is */ }
            }
            return resultJson == "null" ? "(no return value)" : resultJson;
        }
        catch (Exception)
        {
            return "Script error.";
        }
    }

    /// <summary>
    /// Returns cookies visible to one tab's current URL for debugging and tool integrations.
    /// </summary>
    /// <param name="index">Tab index to inspect, or -1 for the active tab.</param>
    /// <returns>A human-readable cookie listing, or an error string.</returns>
    public async Task<string> GetTabCookiesAsync(int index = -1)
    {
        Tab? tab = index < 0 ? ActiveTab : (index < _tabs.Count ? _tabs[index] : null);
        if (tab is null) return "Tab not found.";
        try
        {
            await tab.WebView.EnsureCoreWebView2Async();
            var cookieManager = tab.WebView.CoreWebView2.CookieManager;
            var cookies = await cookieManager.GetCookiesAsync(tab.Url);
            if (cookies.Count == 0) return $"No cookies found for: {tab.Url}";
            var sb = new StringBuilder();
            sb.AppendLine($"Cookies for {tab.Url} ({cookies.Count} total):");
            foreach (var c in cookies)
                sb.AppendLine($"  {c.Name}={c.Value}  [domain={c.Domain}, path={c.Path}, secure={c.IsSecure}, httpOnly={c.IsHttpOnly}]");
            return sb.ToString().TrimEnd();
        }
        catch (Exception)
        {
            return "Error reading cookies.";
        }
    }
}
