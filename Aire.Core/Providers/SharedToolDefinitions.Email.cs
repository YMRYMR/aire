namespace Aire.Providers;

public static partial class SharedToolDefinitions
{
    private static readonly ToolDescriptor[] EmailTools =
    [
        new()
        {
            Name = "read_emails", Category = "email",
            ShortDescription = "Read recent emails from the configured account.",
            Description = "Read recent emails from the configured email account.",
            Parameters = new()
            {
                { "account", new ToolParam("string", "Account display name (optional, uses first account if omitted)") },
                { "count",   new ToolParam("integer","Number of recent emails to retrieve (default 20)") },
            },
            Required = Array.Empty<string>(),
        },
        new()
        {
            Name = "send_email", Category = "email",
            ShortDescription = "Send an email.",
            Description = "Send an email.",
            Parameters = new()
            {
                { "to",      new ToolParam("string", "Recipient email address") },
                { "subject", new ToolParam("string", "Email subject") },
                { "body",    new ToolParam("string", "Email body text") },
                { "account", new ToolParam("string", "Account display name (optional)") },
            },
            Required = ["to", "subject", "body"],
        },
        new()
        {
            Name = "search_emails", Category = "email",
            ShortDescription = "Search emails by keyword.",
            Description = "Search emails by keyword.",
            Parameters = new()
            {
                { "query",   new ToolParam("string", "Search query (searches subject and body)") },
                { "account", new ToolParam("string", "Account display name (optional)") },
            },
            Required = ["query"],
        },
        new()
        {
            Name = "reply_to_email", Category = "email",
            ShortDescription = "Reply to an email by message ID.",
            Description = "Reply to an existing email by message ID.",
            Parameters = new()
            {
                { "message_id", new ToolParam("string", "Message ID from read_emails or search_emails") },
                { "body",       new ToolParam("string", "Reply body text") },
                { "account",    new ToolParam("string", "Account display name (optional)") },
            },
            Required = ["message_id", "body"],
        },
    ];
}
