using System;
using Aire.Services.Mcp;
using static Aire.Services.Tools.ToolHelpers;

namespace Aire.Services.Tools
{
    /// <summary>
    /// Formats tool requests into human-readable descriptions and audit paths.
    /// </summary>
    public static class ToolExecutionDescriptions
    {
        /// <summary>
        /// Builds a human-readable description of a tool request for chat approval UI and trace output.
        /// </summary>
        public static string Describe(
            ToolCallRequest request,
            CommandToolService commandTool,
            CommandExecutionService commandService,
            McpManager mcpManager)
        {
            if (request == null)
                return "Unknown tool";

            return request.Tool switch
            {
                "execute_command"        => commandTool.GetDescription(request, commandService),
                "read_command_output"    => "Read command output",
                "open_url"               => $"Fetch URL: {GetString(request, "url")}",
                "open_browser_tab"       => $"Open in browser: {GetString(request, "url")}",
                "list_directory"         => $"List directory: {GetString(request, "path")}",
                "list_files"             => $"List files: {GetString(request, "path")}",
                "read_file"              => $"Read file: {GetString(request, "path")}",
                "write_file"             => $"Write to: {GetString(request, "path")}",
                "write_to_file"          => $"Write to: {GetString(request, "path")}",
                "apply_diff"             => $"Apply diff to: {GetString(request, "path")}",
                "create_directory"       => $"Create directory: {GetString(request, "path")}",
                "delete_file"            => $"Delete: {GetString(request, "path")}",
                "move_file"              => $"Move: {GetString(request, "from")} \u2192 {GetString(request, "to")}",
                "search_files"           => $"Search \u2018{GetString(request, "pattern")}\u2019 in: {GetString(request, "directory")}",
                "new_task"               => $"New task: {GetString(request, "task")}",
                "request_context"        => $"Request context: {GetString(request, "type")}",
                "attempt_completion"     => $"Complete task: {GetString(request, "result")}",
                "ask_followup_question"  => $"Ask: {GetString(request, "question")}",
                "skill"                  => $"Run skill: {GetString(request, "name")}",
                "switch_mode"            => $"Switch mode: {GetString(request, "mode")}",
                "update_todo_list"       => "Update to-do list",
                "begin_mouse_session"    => $"Begin mouse session ({GetString(request, "duration_minutes")} min)",
                "end_mouse_session"      => "End mouse session",
                "take_screenshot"        => "Take screenshot",
                "mouse_move"             => $"Move mouse to ({GetString(request, "x")}, {GetString(request, "y")})",
                "mouse_click"            => $"{GetString(request, "button")} click at ({GetString(request, "x")}, {GetString(request, "y")})",
                "mouse_double_click"     => $"Double-click at ({GetString(request, "x")}, {GetString(request, "y")})",
                "mouse_drag"             => $"Drag ({GetString(request, "from_x")},{GetString(request, "from_y")}) \u2192 ({GetString(request, "to_x")},{GetString(request, "to_y")})",
                "type_text"              => $"Type: {GetString(request, "text")}",
                "key_press"              => $"Key press: {GetString(request, "key")}",
                "switch_browser_tab"     => $"Switch to browser tab {GetString(request, "index")}",
                "close_browser_tab"      => $"Close browser tab {GetString(request, "index")}",
                "get_browser_html"       => "Get browser tab HTML",
                "execute_browser_script" => $"Run JS: {GetString(request, "script")}",
                "get_browser_cookies"    => "Get browser cookies",
                "get_clipboard"          => "Read clipboard",
                "set_clipboard"          => $"Copy to clipboard: {GetString(request, "text")}",
                "show_notification"      => $"Notify: {GetString(request, "title")}",
                "get_system_info"        => "Get system info",
                "get_running_processes"  => "List running processes",
                "get_active_window"      => "Get active window",
                "get_selected_text"      => "Get selected text",
                "open_file"              => $"Open file: {GetString(request, "path")}",
                "remember"               => $"Remember: {GetString(request, "key")}",
                "recall"                 => $"Recall: {GetString(request, "key")}",
                "set_reminder"           => $"Remind in {GetString(request, "delay_minutes")} min: {GetString(request, "message")}",
                "http_request"           => $"{GetString(request, "method").ToUpperInvariant()} {GetString(request, "url")}",
                "mouse_scroll"           => $"Scroll at ({GetString(request, "x")}, {GetString(request, "y")})",
                "search_file_content"    => $"Search '{GetString(request, "pattern")}' in: {GetString(request, "directory")}",
                "show_image"             => $"Show image: {GetString(request, "path_or_url")}",
                "read_emails"            => "Reading emails",
                "send_email"             => $"Sending email to {GetString(request, "to")}",
                "search_emails"          => $"Searching emails for \"{GetString(request, "query")}\"",
                "reply_to_email"         => "Replying to email",
                var t when mcpManager.IsToolMcp(t) => $"MCP: {request.Tool}",
                _                        => $"Run tool: {request.Tool}"
            };
        }

        /// <summary>
        /// Gets the primary filesystem path affected by a tool request for audit logging.
        /// </summary>
        public static string GetPath(ToolCallRequest request)
        {
            if (request == null)
                return string.Empty;

            return request.Tool switch
            {
                "execute_command"  => GetString(request, "working_directory") is { Length: > 0 } wd ? wd : Environment.CurrentDirectory,
                "list_directory"   => GetString(request, "path"),
                "read_file"        => GetString(request, "path"),
                "write_file"       => GetString(request, "path"),
                "create_directory" => GetString(request, "path"),
                "delete_file"      => GetString(request, "path"),
                "move_file"        => GetString(request, "from"),
                "search_files"     => GetString(request, "directory"),
                _                  => string.Empty
            };
        }
    }
}
