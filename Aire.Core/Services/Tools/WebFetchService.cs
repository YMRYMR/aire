using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Aire.Services;

/// <summary>
/// Fetches a web page (or RSS/Atom feed) over HTTP(S) and converts it to
/// readable plain text so the AI can read articles, documentation, and news.
/// </summary>
public sealed class WebFetchService : IDisposable
{
    private readonly HttpClient _http;

    public WebFetchService()
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect        = true,
            MaxAutomaticRedirections = 10,
            UseCookies               = true,
        };
        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(25) };
        _http.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        _http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
            "AppleWebKit/537.36 (KHTML, like Gecko) " +
            "Chrome/124.0.0.0 Safari/537.36");
    }

    /// <summary>
    /// Fetches <paramref name="url"/> and returns its plain-text content.
    /// Automatically handles HTML pages and RSS/Atom feeds.
    /// </summary>
    /// <param name="url">Absolute URL to fetch (https:// added if missing).</param>
    /// <param name="maxChars">Maximum characters to return (default: 12 000).</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<WebFetchResult> FetchAsync(
        string url,
        int maxChars = 12_000,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            return WebFetchResult.Error("URL is required.", url);

        // Ensure scheme is present
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            url = "https://" + url;

        string body;
        string finalUrl;
        string contentType;
        HttpStatusCode statusCode;

        try
        {
            using var response = await _http.GetAsync(url, ct);
            finalUrl    = response.RequestMessage?.RequestUri?.ToString() ?? url;
            statusCode  = response.StatusCode;
            contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;

            if (!response.IsSuccessStatusCode)
            {
                return BuildHttpErrorResult(url, finalUrl, statusCode);
            }

            body = await response.Content.ReadAsStringAsync(ct);
        }
        catch (TaskCanceledException)
        {
            return WebFetchResult.Error("Request timed out after 25 seconds.", url);
        }
        catch (Exception ex)
        {
            return WebFetchResult.Error($"Network error: {ex.Message}", url);
        }

        // ── Dispatch by content type ──────────────────────────────────────
        string title, text;

        bool isFeed = contentType.Contains("rss", StringComparison.OrdinalIgnoreCase)
                   || contentType.Contains("atom", StringComparison.OrdinalIgnoreCase)
                   || contentType.Contains("xml", StringComparison.OrdinalIgnoreCase)
                   || body.TrimStart().StartsWith("<?xml", StringComparison.OrdinalIgnoreCase)
                   || body.TrimStart().StartsWith("<rss", StringComparison.OrdinalIgnoreCase);

        try
        {
            if (isFeed)
            {
                (title, text) = ParseFeed(body);
            }
            else
            {
                title = ExtractTitle(body);
                text  = HtmlToText(body);
            }
        }
        catch (Exception ex)
        {
            return WebFetchResult.Error($"Failed to parse page content: {ex.Message}", finalUrl);
        }

        // ── Truncate ──────────────────────────────────────────────────────
        bool truncated = false;
        if (text.Length > maxChars)
        {
            text      = text[..maxChars];
            truncated = true;
        }

        return new WebFetchResult
        {
            Url       = finalUrl,
            Title     = title,
            Text      = text,
            Truncated = truncated,
            MaxChars  = maxChars,
        };
    }

    // ── HTTP error helper ────────────────────────────────────────────────────

    private static WebFetchResult BuildHttpErrorResult(string requestedUrl, string finalUrl, HttpStatusCode status)
    {
        var code     = (int)status;
        var baseHost = TryGetHost(requestedUrl);

        var msg = code switch
        {
            403 or 429 =>
                $"HTTP {code} — the site is blocking automated access (bot protection / rate limit).\n\n" +
                $"Try one of these alternatives instead:\n" +
                $"• RSS feed:  {baseHost}/rss  or  {baseHost}/feed  or  {baseHost}/rss.xml\n" +
                $"• Atom feed: {baseHost}/atom.xml\n" +
                $"Pick the one that works and call open_url again with that URL.",
            404 =>
                $"HTTP 404 — page not found at {finalUrl}.",
            _ =>
                $"HTTP {code} ({status}) fetching {finalUrl}.",
        };

        return WebFetchResult.Error(msg, requestedUrl);
    }

    private static string TryGetHost(string url)
    {
        try { return new Uri(url).GetLeftPart(UriPartial.Authority); }
        catch { return url; }
    }

    // ── RSS / Atom feed parser ───────────────────────────────────────────────

    private static (string title, string text) ParseFeed(string xml)
    {
        try
        {
            var doc  = XDocument.Parse(xml);
            XNamespace atom = "http://www.w3.org/2005/Atom";

            bool isAtom = doc.Root?.Name.LocalName == "feed";

            var sb        = new StringBuilder();
            string feedTitle;

            if (isAtom)
            {
                feedTitle = (string?)doc.Root?.Element(atom + "title") ?? "Feed";
                sb.AppendLine($"# {feedTitle}");
                sb.AppendLine();
                foreach (var entry in doc.Root!.Elements(atom + "entry").Take(20))
                {
                    var t    = StripTags((string?)entry.Element(atom + "title") ?? string.Empty);
                    var link = (string?)entry.Elements(atom + "link")
                                             .FirstOrDefault(e => (string?)e.Attribute("rel") != "replies")
                                             ?.Attribute("href") ?? string.Empty;
                    var summ = StripTags(
                        (string?)entry.Element(atom + "summary")
                        ?? (string?)entry.Element(atom + "content")
                        ?? string.Empty);

                    sb.AppendLine($"Title: {DecodeEntities(t)}");
                    if (!string.IsNullOrEmpty(link)) sb.AppendLine($"Link:  {link}");
                    if (!string.IsNullOrEmpty(summ)) sb.AppendLine($"       {Truncate(DecodeEntities(summ), 280)}");
                    sb.AppendLine();
                }
            }
            else
            {
                var channel = doc.Root?.Element("channel");
                feedTitle = (string?)channel?.Element("title") ?? "Feed";
                sb.AppendLine($"# {feedTitle}");
                sb.AppendLine();
                foreach (var item in (channel?.Elements("item") ?? Enumerable.Empty<XElement>()).Take(20))
                {
                    var t    = StripTags((string?)item.Element("title") ?? string.Empty);
                    var link = (string?)item.Element("link") ?? string.Empty;
                    var desc = StripTags(
                        (string?)item.Element("description")
                        ?? (string?)item.Element("{http://purl.org/rss/1.0/modules/content/}encoded")
                        ?? string.Empty);

                    sb.AppendLine($"Title: {DecodeEntities(t)}");
                    if (!string.IsNullOrEmpty(link)) sb.AppendLine($"Link:  {link}");
                    if (!string.IsNullOrEmpty(desc)) sb.AppendLine($"       {Truncate(DecodeEntities(desc), 280)}");
                    sb.AppendLine();
                }
            }

            return (feedTitle, sb.ToString().Trim());
        }
        catch
        {
            return (string.Empty, HtmlToText(xml));
        }
    }

    // ── HTML extraction helpers ──────────────────────────────────────────────

    private static readonly Regex _titleRx      = new(@"<title[^>]*>(.*?)</title>",  RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex _scriptRx     = new(@"<script[^>]*>.*?</script>",  RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex _styleRx      = new(@"<style[^>]*>.*?</style>",    RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex _headRx       = new(@"<head[^>]*>.*?</head>",      RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex _commentRx    = new(@"<!--.*?-->",                 RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex _tagRx        = new(@"<[^>]+>",                    RegexOptions.IgnoreCase);
    private static readonly Regex _whitespaceRx = new(@"[ \t]+");
    private static readonly Regex _newlineRx    = new(@"\n{3,}");
    private static readonly Regex _blockRx      = new(
        @"<(p|div|section|article|header|footer|main|h[1-6]|li|tr|td|th|br|hr|blockquote)[^>]*>",
        RegexOptions.IgnoreCase);

    internal static string ExtractTitle(string html)
    {
        var m = _titleRx.Match(html);
        return m.Success ? DecodeEntities(StripTags(m.Groups[1].Value)).Trim() : string.Empty;
    }

    internal static string HtmlToText(string html)
    {
        html = _headRx.Replace(html, " ");
        html = _commentRx.Replace(html, " ");
        html = _scriptRx.Replace(html, " ");
        html = _styleRx.Replace(html, " ");
        html = _blockRx.Replace(html, "\n");
        html = Regex.Replace(html, @"</(p|div|section|article|h[1-6]|li|blockquote)>",
                              "\n", RegexOptions.IgnoreCase);
        html = _tagRx.Replace(html, string.Empty);
        html = DecodeEntities(html);
        html = _whitespaceRx.Replace(html, " ");
        html = html.Replace("\r\n", "\n").Replace("\r", "\n");
        html = _newlineRx.Replace(html, "\n\n");
        return html.Trim();
    }

    private static string StripTags(string s) => _tagRx.Replace(s, string.Empty);

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max].TrimEnd() + "\u2026";

    internal static string DecodeEntities(string s)
    {
        s = s.Replace("&amp;",   "&")
             .Replace("&lt;",    "<")
             .Replace("&gt;",    ">")
             .Replace("&quot;",  "\"")
             .Replace("&apos;",  "'")
             .Replace("&nbsp;",  " ")
             .Replace("&mdash;", "\u2014")
             .Replace("&ndash;", "\u2013")
             .Replace("&lsquo;", "\u2018")
             .Replace("&rsquo;", "\u2019")
             .Replace("&ldquo;", "\u201C")
             .Replace("&rdquo;", "\u201D")
             .Replace("&hellip;","\u2026");

        s = Regex.Replace(s, @"&#x([0-9a-fA-F]+);",
            m => ((char)Convert.ToInt32(m.Groups[1].Value, 16)).ToString());
        s = Regex.Replace(s, @"&#([0-9]+);",
            m =>
            {
                var n = int.Parse(m.Groups[1].Value);
                return n is >= 32 and < 0xFFFF ? ((char)n).ToString() : string.Empty;
            });

        return s;
    }

    public void Dispose() => _http.Dispose();
}

// ── Result ───────────────────────────────────────────────────────────────────

public sealed class WebFetchResult
{
    public bool    Success      { get; init; } = true;
    public string  Url          { get; init; } = string.Empty;
    public string  Title        { get; init; } = string.Empty;
    public string  Text         { get; init; } = string.Empty;
    public bool    Truncated    { get; init; }
    public int     MaxChars     { get; init; }
    public string? ErrorMessage { get; init; }

    public static WebFetchResult Error(string message, string url = "") => new()
    {
        Success      = false,
        Url          = url,
        ErrorMessage = message,
        Text         = string.Empty,
    };

    /// <summary>Returns the result formatted as a tool response string.</summary>
    public string ToToolResponseString()
    {
        if (!Success)
            return $"FAILED: {ErrorMessage}";

        var sb = new StringBuilder();
        sb.AppendLine($"URL: {Url}");
        if (!string.IsNullOrEmpty(Title))
            sb.AppendLine($"Title: {Title}");
        sb.AppendLine();
        sb.Append(Text);
        if (Truncated)
            sb.AppendLine(
                $"\n\n[Content truncated to {MaxChars:N0} characters. " +
                $"Call open_url again with max_chars set higher if you need more.]");
        return sb.ToString();
    }
}
