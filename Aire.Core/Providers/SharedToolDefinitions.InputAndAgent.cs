namespace Aire.Providers;

public static partial class SharedToolDefinitions
{
    private static readonly ToolDescriptor[] KeyboardTools =
    [
        new()
        {
            Name = "begin_keyboard_session", Category = "keyboard",
            Description = "Request permission to control the keyboard for a limited time. Must be called before key_press, key_combo, or type_text.",
            Parameters = new() { { "duration_minutes", new ToolParam("integer", "Session length in minutes (default: 10)") } },
            Required = Array.Empty<string>(),
        },
        new()
        {
            Name = "end_keyboard_session", Category = "keyboard",
            Description = "End the active keyboard control session.",
            Parameters = new(),
            Required = Array.Empty<string>(),
        },
        new()
        {
            Name = "key_press", Category = "keyboard",
            Description = "Press a single keyboard key. Requires an active keyboard session.",
            Parameters = new() { { "key", new ToolParam("string", "Key name: Enter, Tab, Escape, Backspace, Delete, Home, End, PageUp, PageDown, Left, Up, Right, Down, Ctrl, Alt, Shift, Win, Space, F1-F12, or any single character.") } },
            Required = ["key"],
        },
        new()
        {
            Name = "key_combo", Category = "keyboard",
            Description = "Press a keyboard shortcut / key combination simultaneously (e.g. Ctrl+N, Alt+Tab, Ctrl+Shift+S). " +
                          "PREFER this over mouse clicks whenever possible. " +
                          "Pass keys as an array in the order they should be held down.",
            Parameters = new() { { "keys", new ToolParam("array", "Keys to hold simultaneously, e.g. [\"ctrl\",\"c\"]") { Items = new ToolParam("string", "") } } },
            Required = ["keys"],
        },
        new()
        {
            Name = "type_text", Category = "keyboard",
            Description = "Type a string of text as keyboard input. Requires an active keyboard session.",
            Parameters = new() { { "text", new ToolParam("string", "The text to type") } },
            Required = ["text"],
        },
    ];

    private static readonly ToolDescriptor[] MouseTools =
    [
        new()
        {
            Name = "begin_mouse_session", Category = "mouse",
            Description = "Request permission to control the mouse and keyboard for a limited time. " +
                          "The user must approve once. After approval, use mouse/keyboard tools freely until end_mouse_session.",
            Parameters = new() { { "duration_minutes", new ToolParam("integer", "How long the session should last (default: 5 minutes)") } },
            Required = Array.Empty<string>(),
        },
        new()
        {
            Name = "end_mouse_session", Category = "mouse",
            Description = "End the active mouse control session.",
            Parameters = new(),
            Required = Array.Empty<string>(),
        },
        new()
        {
            Name = "take_screenshot", Category = "mouse",
            Description = "Capture a screenshot of the current screen and return it for analysis. Use this to see the current state before and after interactions.",
            Parameters = new(),
            Required = Array.Empty<string>(),
        },
        new()
        {
            Name = "mouse_move", Category = "mouse",
            Description = "Move the mouse cursor to screen coordinates (pixels, top-left = 0,0). Requires an active mouse session.",
            Parameters = new()
            {
                { "x", new ToolParam("integer", "Horizontal screen coordinate") },
                { "y", new ToolParam("integer", "Vertical screen coordinate") },
            },
            Required = ["x", "y"],
        },
        new()
        {
            Name = "mouse_click", Category = "mouse",
            Description = "Click the mouse at screen coordinates. button: left (default), right, middle. Requires an active mouse session.",
            Parameters = new()
            {
                { "x",      new ToolParam("integer", "X coordinate") },
                { "y",      new ToolParam("integer", "Y coordinate") },
                { "button", new ToolParam("string",  "left (default), right, or middle") },
            },
            Required = ["x", "y"],
        },
        new()
        {
            Name = "mouse_double_click", Category = "mouse",
            Description = "Double-click at screen coordinates. Requires an active mouse session.",
            Parameters = new()
            {
                { "x", new ToolParam("integer", "X coordinate") },
                { "y", new ToolParam("integer", "Y coordinate") },
            },
            Required = ["x", "y"],
        },
        new()
        {
            Name = "mouse_drag", Category = "mouse",
            Description = "Click and drag from one screen position to another. Requires an active mouse session.",
            Parameters = new()
            {
                { "from_x", new ToolParam("integer", "Start X") },
                { "from_y", new ToolParam("integer", "Start Y") },
                { "to_x",   new ToolParam("integer", "End X") },
                { "to_y",   new ToolParam("integer", "End Y") },
            },
            Required = ["from_x", "from_y", "to_x", "to_y"],
        },
        new()
        {
            Name = "mouse_scroll", Category = "mouse",
            Description =
                "Scroll the mouse wheel at screen coordinates. " +
                "Positive delta scrolls up, negative scrolls down. " +
                "Requires an active mouse session.",
            Parameters = new()
            {
                { "x",     new ToolParam("integer", "X coordinate to scroll at") },
                { "y",     new ToolParam("integer", "Y coordinate to scroll at") },
                { "delta", new ToolParam("integer", "Scroll amount: positive = up, negative = down (default: 3)") },
            },
            Required = ["x", "y"],
        },
    ];

    private static readonly ToolDescriptor[] AgentTools =
    [
        new()
        {
            Name        = "switch_model",
            Category    = "agent",
            Description =
                "Switch to a different AI model mid-conversation. " +
                "Use direction=\"up\" when the task needs more capability (complex reasoning, coding), " +
                "\"down\" to scale back to a cheaper/faster model for simple follow-ups, " +
                "or \"lateral\" to switch providers. " +
                "model_name must exactly match a name from the model list in the system prompt.",
            Parameters  = new()
            {
                { "model_name", new ToolParam("string", "Exact model name from the available model list") },
                { "reason",     new ToolParam("string", "Brief reason for switching") },
                { "direction",  new ToolParam("string", "up, down, or lateral") },
            },
            Required    = ["model_name", "reason"],
        },
        new()
        {
            Name        = "update_todo_list",
            Category    = "agent",
            Description =
                "Replace the current to-do list shown in the chat with the supplied items. " +
                "Call this at the start of a multi-step task to show progress, then update statuses as steps complete. " +
                "status values: pending (not started), in_progress (active), completed (done), blocked (waiting).",
            Parameters  = new()
            {
                { "tasks", new ToolParam("array",  "List of task objects")
                    { Items = new ToolParam("object", "{id, description, status, subtasks?}") } },
            },
            Required    = ["tasks"],
        },
        new()
        {
            Name        = "ask_followup_question",
            Category    = "agent",
            Description =
                "Ask the user a clarifying question and wait for their answer before continuing. " +
                "Provide predefined options so the user can click a button instead of typing. " +
                "Use this when you genuinely need user input to proceed; do NOT use it to confirm actions you already know to take.",
            Parameters  = new()
            {
                { "question", new ToolParam("string", "The question to ask the user") },
                { "options",  new ToolParam("array",  "Predefined answer choices the user can click")
                    { Items = new ToolParam("string", "One answer option") } },
            },
            Required    = ["question"],
        },
        new()
        {
            Name        = "attempt_completion",
            Category    = "agent",
            Description =
                "Signal that the user's entire request is now fulfilled. " +
                "Include a short summary of what was accomplished. " +
                "Only call this when ALL steps of the task are complete.",
            Parameters  = new()
            {
                { "result", new ToolParam("string", "Brief summary of what was accomplished") },
            },
            Required    = ["result"],
        },
        new()
        {
            Name        = "show_image",
            Category    = "agent",
            Description =
                "Display an image in the chat for the user to see. " +
                "Accepts a local file path (e.g. C:/images/photo.png) or a public image URL. " +
                "Use this whenever the AI wants to show the user a picture, diagram, chart, " +
                "generated image, or any visual content. " +
                "An optional caption appears below the image.",
            Parameters  = new()
            {
                { "path_or_url", new ToolParam("string", "Absolute local file path or https:// image URL") },
                { "caption",     new ToolParam("string", "Optional caption shown below the image") },
            },
            Required    = ["path_or_url"],
        },
    ];
}
