namespace Aire.Providers;

public static partial class SharedToolDefinitions
{
    private static readonly ToolDescriptor[] BrowserTools =
    [
        new()
        {
            Name = "open_url",
            Category = "browser",
            ShortDescription = "Fetch a web page and return its readable text content (background HTTP fetch, no browser window).",
            Description =
                "Fetch a web page and return its readable text content. " +
                "Use this to read articles, documentation, Wikipedia, Stack Overflow, GitHub pages, " +
                "news, weather, or any publicly accessible web page. " +
                "Returns the plain text of the page (HTML tags removed). " +
                "For very long pages set max_chars higher (up to 50 000). " +
                "ALWAYS use this instead of telling the user to open a browser themselves.",
            Parameters = new()
            {
                { "url",       new ToolParam("string",  "Absolute URL to fetch (https:// will be added if missing)") },
                { "max_chars", new ToolParam("integer", "Maximum characters to return (default: 12 000, max: 50 000)") },
            },
            Required = ["url"],
        },
        new()
        {
            Name     = "open_browser_tab",
            Category = "browser",
            ShortDescription = "Open a URL in the Aire visible browser window.",
            Description =
                "Opens a URL in the Aire in-app browser window (WebView). " +
                "Use this whenever the user wants to visually open or navigate to a page. " +
                "If the browser is already open, the active tab navigates to the URL. " +
                "Otherwise a new browser window opens. " +
                "This is different from open_url, which silently fetches the page in the background without showing any window.",
            Parameters = new()
            {
                { "url", new ToolParam("string", "The full URL to open (must start with http:// or https://).") },
            },
            Required = ["url"],
        },
        new()
        {
            Name     = "list_browser_tabs",
            Category = "browser",
            ShortDescription = "List all open browser tabs (index, URL, title).",
            Description =
                "Lists all tabs currently open in the Aire browser window. " +
                "Returns the index, URL, and title of each tab, and which one is active. " +
                "Use this before read_browser_tab to find the right index.",
            Parameters = new(),
            Required   = Array.Empty<string>(),
        },
        new()
        {
            Name     = "read_browser_tab",
            Category = "browser",
            ShortDescription = "Read the rendered text of a browser tab the user has open.",
            Description =
                "Reads the full visible text content of a browser tab that the user has open. " +
                "Use list_browser_tabs first to see available tabs. " +
                "Unlike open_url (which does a background HTTP fetch), this reads what the browser has " +
                "already rendered — useful for JavaScript-heavy pages, pages the user is already on, " +
                "or when you want to analyze something the user is looking at.",
            Parameters = new()
            {
                { "index", new ToolParam("integer", "Tab index from list_browser_tabs. Omit or pass -1 for the active tab.") },
            },
            Required = Array.Empty<string>(),
        },
        new()
        {
            Name     = "switch_browser_tab",
            Category = "browser",
            ShortDescription = "Switch the active browser tab by index.",
            Description =
                "Switch the active browser tab by index. " +
                "Use list_browser_tabs first to get the correct index.",
            Parameters = new()
            {
                { "index", new ToolParam("integer", "Tab index from list_browser_tabs") },
            },
            Required = ["index"],
        },
        new()
        {
            Name     = "close_browser_tab",
            Category = "browser",
            ShortDescription = "Close a browser tab (-1 for active tab).",
            Description =
                "Close a browser tab by index (-1 or omit for the active tab). " +
                "If it is the last tab the browser window closes.",
            Parameters = new()
            {
                { "index", new ToolParam("integer", "Tab index to close (-1 = active tab)") },
            },
            Required = Array.Empty<string>(),
        },
        new()
        {
            Name     = "get_browser_html",
            Category = "browser",
            ShortDescription = "Get the full HTML source of a browser tab.",
            Description =
                "Get the full HTML source of a browser tab. " +
                "Unlike read_browser_tab (which returns plain text), this returns the raw HTML " +
                "including tags — useful for scraping, form inspection, or reading JS-rendered content.",
            Parameters = new()
            {
                { "index", new ToolParam("integer", "Tab index (-1 or omit for active tab)") },
            },
            Required = Array.Empty<string>(),
        },
        new()
        {
            Name     = "execute_browser_script",
            Category = "browser",
            ShortDescription = "Execute JavaScript in a browser tab and return the result.",
            Description =
                "Execute JavaScript in a browser tab and return the result. " +
                "Use this to read DOM state, manipulate the page, fill forms, submit forms, " +
                "click elements, or extract structured data — much faster than mouse automation. " +
                "The script runs in the page's own JavaScript context. " +
                "Use 'return value' to return data. Example: return document.title",
            Parameters = new()
            {
                { "script", new ToolParam("string",  "JavaScript code to execute. Use 'return expr' to return a value.") },
                { "index",  new ToolParam("integer", "Tab index (-1 or omit for active tab)") },
            },
            Required = ["script"],
        },
        new()
        {
            Name     = "get_browser_cookies",
            Category = "browser",
            ShortDescription = "List cookies for the active browser tab's domain.",
            Description =
                "List all cookies for the active tab's domain. " +
                "Returns name, value, domain, path, secure flag, and httpOnly flag for each cookie.",
            Parameters = new()
            {
                { "index", new ToolParam("integer", "Tab index (-1 or omit for active tab)") },
            },
            Required = Array.Empty<string>(),
        },
    ];
}
