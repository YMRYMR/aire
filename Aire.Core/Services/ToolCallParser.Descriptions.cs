using System;
using System.Text.Json;

namespace Aire.Services
{
    public static partial class ToolCallParser
    {
        private static string T(string key, string fallback) =>
            LocalizationService.S(key, fallback);

        private static string BuildDescription(string tool, JsonElement root) => tool switch
        {
            "list_directory" => Format("toolDesc.list_directory", "Look in {0}/?", FileName(GetStr(root, "path"))),
            "list_files" => Format("toolDesc.list_files", "Look in {0}/?", FileName(GetStr(root, "path"))),
            "read_file" => Format("toolDesc.read_file", "Read {0}?", FileName(GetStr(root, "path"))),
            "write_file" => Format("toolDesc.write_file", "Write to {0}?", FileName(GetStr(root, "path"))),
            "write_to_file" => Format("toolDesc.write_to_file", "Write to {0}?", FileName(GetStr(root, "path"))),
            "apply_diff" => Format("toolDesc.apply_diff", "Edit {0}?", FileName(GetStr(root, "path"))),
            "create_directory" => Format("toolDesc.create_directory", "Create folder {0}?", FileName(GetStr(root, "path"))),
            "delete_file" => Format("toolDesc.delete_file", "Delete {0}?", FileName(GetStr(root, "path"))),
            "move_file" => Format("toolDesc.move_file", "Move {0} → {1}?", FileName(GetStr(root, "from")), FileName(GetStr(root, "to"))),
            "search_files" => Format("toolDesc.search_files", "Search for '{0}' in {1}/?", GetStr(root, "pattern"), FileName(GetStr(root, "directory"))),
            "execute_command" => DescribeCommand(GetStr(root, "command")),
            "read_command_output" => T("toolDesc.read_command_output", "Read command output?"),
            "open_url" => Format("toolDesc.open_url", "Fetch {0}?", GetStr(root, "url")),
            "web_fetch" => Format("toolDesc.web_fetch", "Fetch {0}?", GetStr(root, "url")),
            "open_browser_tab" => Format("toolDesc.open_browser_tab", "Open {0}?", GetStr(root, "url")),
            "list_browser_tabs" => T("toolDesc.list_browser_tabs", "List open browser tabs?"),
            "read_browser_tab" => T("toolDesc.read_browser_tab", "Read current browser tab?"),
            "execute_browser_script" => DescribeBrowserScript(GetStr(root, "script")),
            "get_clipboard" => T("toolDesc.get_clipboard", "Ask Aire to read the clipboard?"),
            "set_clipboard" => Format("toolDesc.set_clipboard", "Ask Aire to copy this text to the clipboard: {0}?", Truncate(GetStr(root, "text"), 60)),
            "show_notification" => Format("toolDesc.show_notification", "Ask Aire to show a notification: {0}?", Truncate(GetStr(root, "title"), 40)),
            "get_system_info" => T("toolDesc.get_system_info", "Ask Aire to check this computer's system information?"),
            "get_running_processes" => T("toolDesc.get_running_processes", "Ask Aire to list the running processes?"),
            "get_active_window" => T("toolDesc.get_active_window", "Ask Aire which window is currently active?"),
            "get_selected_text" => T("toolDesc.get_selected_text", "Ask Aire to read the selected text?"),
            "new_task" => Format("toolDesc.new_task", "Start task: {0}?", Truncate(GetStr(root, "task"), 60)),
            "attempt_completion" => T("toolDesc.attempt_completion", "Finish up?"),
            "ask_followup_question" => Format("toolDesc.ask_followup_question", "Ask: {0}?", Truncate(GetStr(root, "question"), 60)),
            "skill" => Format("toolDesc.skill", "Run skill: {0}?", GetStr(root, "name")),
            "switch_model" => Format("toolDesc.switch_model", "Switch to {0}?", GetStr(root, "model_name")),
            "switch_mode" => Format("toolDesc.switch_mode", "Switch to {0} mode?", GetStr(root, "mode")),
            "update_todo_list" => T("toolDesc.update_todo_list", "Update to-do list?"),
            "begin_keyboard_session" => T("toolDesc.begin_keyboard_session", "Start keyboard session?"),
            "end_keyboard_session" => T("toolDesc.end_keyboard_session", "End keyboard session?"),
            "type_text" => Format("toolDesc.type_text", "Type \"{0}\"?", Truncate(GetStr(root, "text"), 40)),
            "key_press" => Format("toolDesc.key_press", "Press {0}?", GetStr(root, "key")),
            "key_combo" => Format("toolDesc.key_combo", "Press {0}?", GetStr(root, "keys")),
            "begin_mouse_session" => Format("toolDesc.begin_mouse_session", "Start mouse session ({0} min)?", GetStr(root, "duration_minutes")),
            "end_mouse_session" => T("toolDesc.end_mouse_session", "End mouse session?"),
            "take_screenshot" => T("toolDesc.take_screenshot", "Take a screenshot?"),
            "mouse_move" => Format("toolDesc.mouse_move", "Move mouse to ({0}, {1})?", GetStr(root, "x"), GetStr(root, "y")),
            "mouse_click" => Format("toolDesc.mouse_click", "Click at ({0}, {1})?", GetStr(root, "x"), GetStr(root, "y")),
            "mouse_double_click" => Format("toolDesc.mouse_double_click", "Double-click at ({0}, {1})?", GetStr(root, "x"), GetStr(root, "y")),
            "mouse_drag" => Format("toolDesc.mouse_drag", "Drag ({0},{1}) → ({2},{3})?", GetStr(root, "from_x"), GetStr(root, "from_y"), GetStr(root, "to_x"), GetStr(root, "to_y")),
            "show_image" => Format("toolDesc.show_image", "Show image {0}?", Truncate(GetStr(root, "path_or_url"), 60)),
            "read_emails" => T("toolDesc.read_emails", "Ask Aire to read emails?"),
            "send_email" => Format("toolDesc.send_email", "Ask Aire to send an email to {0}?", Truncate(GetStr(root, "to"), 40)),
            "search_emails" => Format("toolDesc.search_emails", "Ask Aire to search emails for '{0}'?", Truncate(GetStr(root, "query"), 40)),
            "reply_to_email" => T("toolDesc.reply_to_email", "Ask Aire to reply to an email?"),
            _ when tool.StartsWith("mcp:", StringComparison.OrdinalIgnoreCase) => Format("toolDesc.mcp", "Ask Aire to use MCP tool '{0}'?", tool),
            _ => Format("toolDesc.default", "Ask Aire to use tool '{0}'?", tool)
        };

        private static string Format(string key, string fallback, params object[] args) =>
            string.Format(T(key, fallback), args);

        private static string FileName(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            var name = System.IO.Path.GetFileName(path.TrimEnd('/', '\\'));
            return string.IsNullOrEmpty(name) ? path : name;
        }

        private static string Truncate(string s, int max) =>
            s.Length <= max ? s : s[..max] + "\u2026";

        private static string DescribeCommand(string cmd)
        {
            if (string.IsNullOrWhiteSpace(cmd))
                return T("toolDesc.execute_command_default", "Run a command?");

            var trimmed = cmd.Trim();
            if (!trimmed.Contains(' ') && !trimmed.Contains('&') && !trimmed.Contains('|'))
            {
                var appName = System.IO.Path.GetFileNameWithoutExtension(trimmed);
                return Format("toolDesc.execute_command_open", "Open {0}?", Capitalize(appName));
            }

            return Format("toolDesc.execute_command_run", "Run: {0}?", Truncate(trimmed, 60));
        }

        private static string DescribeBrowserScript(string script)
        {
            if (string.IsNullOrWhiteSpace(script))
                return T("toolDesc.execute_browser_script_default", "Run browser script?");

            return T("toolDesc.execute_browser_script", $"Run browser script:\n\n```js\n{script.Trim()}\n```");
        }

        private static string Capitalize(string s) =>
            string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s[1..];
    }
}
