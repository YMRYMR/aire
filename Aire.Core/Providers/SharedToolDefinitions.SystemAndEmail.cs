namespace Aire.Providers;

public static partial class SharedToolDefinitions
{
    private static readonly ToolDescriptor[] SystemTools =
    [
        new()
        {
            Name        = "get_clipboard",
            Category    = "system",
            ShortDescription = "Read the clipboard text.",
            Description = "Read the current text content of the system clipboard.",
            Parameters  = new(),
            Required    = Array.Empty<string>(),
        },
        new()
        {
            Name        = "set_clipboard",
            Category    = "system",
            ShortDescription = "Write text to the clipboard.",
            Description =
                "Write text to the system clipboard so the user can paste it anywhere. " +
                "Use this to hand off results, code, or content to the user.",
            Parameters  = new() { { "text", new ToolParam("string", "Text to place on the clipboard") } },
            Required    = ["text"],
        },
        new()
        {
            Name        = "show_notification",
            Category    = "system",
            ShortDescription = "Show a Windows desktop notification.",
            Description =
                "Show a Windows desktop notification (balloon tip from the system tray). " +
                "Use this to signal task completion or to alert the user of something important " +
                "when they may not be looking at the chat.",
            Parameters  = new()
            {
                { "title",   new ToolParam("string", "Notification title (keep short)") },
                { "message", new ToolParam("string", "Notification body text") },
            },
            Required = ["title", "message"],
        },
        new()
        {
            Name        = "get_system_info",
            Category    = "system",
            ShortDescription = "Return OS, CPU, RAM, disk, and uptime info.",
            Description =
                "Return OS version, CPU count, total RAM, available RAM, disk space, and system uptime. " +
                "Use this before diagnosing performance issues or checking available resources.",
            Parameters  = new(),
            Required    = Array.Empty<string>(),
        },
        new()
        {
            Name        = "get_running_processes",
            Category    = "system",
            ShortDescription = "List running processes sorted by memory usage.",
            Description =
                "List running processes sorted by memory usage. " +
                "Useful for diagnosing slowdowns, finding hung apps, or checking if a program is running.",
            Parameters  = new()
            {
                { "top_n",  new ToolParam("integer", "Number of top processes to return (default: 20)") },
                { "filter", new ToolParam("string",  "Only show processes whose name contains this string (optional)") },
            },
            Required = Array.Empty<string>(),
        },
        new()
        {
            Name        = "get_active_window",
            Category    = "system",
            ShortDescription = "Return the title and process of the foreground window.",
            Description =
                "Return the title and process name of the currently focused (foreground) window. " +
                "Use this to understand which application the user is working in.",
            Parameters  = new(),
            Required    = Array.Empty<string>(),
        },
        new()
        {
            Name        = "get_selected_text",
            Category    = "system",
            ShortDescription = "Get the text currently selected in any application.",
            Description =
                "Get the text currently selected in the active application by simulating Ctrl+C. " +
                "This is the fastest way to have the user hand text to the AI without copy-pasting.",
            Parameters  = new(),
            Required    = Array.Empty<string>(),
        },
        new()
        {
            Name        = "open_file",
            Category    = "system",
            ShortDescription = "Open a file with its default application.",
            Description =
                "Open a file with its default associated application, " +
                "exactly like double-clicking it in Windows Explorer.",
            Parameters  = new() { { "path", new ToolParam("string", "Absolute path to the file to open") } },
            Required    = ["path"],
        },
        new()
        {
            Name        = "remember",
            Category    = "system",
            ShortDescription = "Persist a named fact across conversations (empty value deletes it).",
            Description =
                "Store a named fact or note that persists across conversations. " +
                "Use this to remember user preferences, names, project details, etc. " +
                "Pass an empty string as value to delete the key.",
            Parameters  = new()
            {
                { "key",   new ToolParam("string", "Name for the stored value") },
                { "value", new ToolParam("string", "Value to store. Pass empty string to delete.") },
            },
            Required = ["key", "value"],
        },
        new()
        {
            Name        = "recall",
            Category    = "system",
            ShortDescription = "Retrieve a stored fact by key (empty key lists all).",
            Description =
                "Retrieve a previously stored fact by key. " +
                "Pass an empty key to list all stored keys.",
            Parameters  = new() { { "key", new ToolParam("string", "Key to retrieve. Leave empty to list all.") } },
            Required    = Array.Empty<string>(),
        },
        new()
        {
            Name        = "set_reminder",
            Category    = "system",
            ShortDescription = "Show a desktop notification after a delay.",
            Description =
                "Show a desktop notification after a delay. Returns immediately; fires in the background. " +
                "Use for 'remind me in X minutes' requests.",
            Parameters  = new()
            {
                { "message",       new ToolParam("string", "Reminder message to show") },
                { "delay_minutes", new ToolParam("number", "Minutes from now (can be fractional, e.g. 0.5 for 30 seconds)") },
            },
            Required = ["message", "delay_minutes"],
        },
        new()
        {
            Name        = "http_request",
            Category    = "browser",
            ShortDescription = "Make a custom HTTP request (REST APIs, webhooks). Returns the raw response.",
            Description =
                "Make an HTTP request with custom method, headers, and body. " +
                "Use this for REST API calls, webhooks, or form submissions. " +
                "Unlike open_url (which fetches readable page text), this gives you full control " +
                "over the request and returns the raw response body.",
            Parameters  = new()
            {
                { "url",     new ToolParam("string", "Full URL including https://") },
                { "method",  new ToolParam("string", "HTTP method: GET (default), POST, PUT, PATCH, DELETE") },
                { "headers", new ToolParam("string", "Optional JSON object of header name→value pairs") },
                { "body",    new ToolParam("string", "Optional request body (for POST/PUT/PATCH)") },
            },
            Required = ["url"],
        },
    ];
}
