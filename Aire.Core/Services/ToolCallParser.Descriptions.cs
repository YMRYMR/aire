using System;
using System.Text.Json;

namespace Aire.Services
{
    public static partial class ToolCallParser
    {
        private static string BuildDescription(string tool, JsonElement root) => tool switch
        {
            "list_directory" => $"Looking in {FileName(GetStr(root, "path"))}/",
            "list_files" => $"Looking in {FileName(GetStr(root, "path"))}/",
            "read_file" => $"Reading {FileName(GetStr(root, "path"))}",
            "write_file" => $"Writing to {FileName(GetStr(root, "path"))}",
            "write_to_file" => $"Writing to {FileName(GetStr(root, "path"))}",
            "apply_diff" => $"Editing {FileName(GetStr(root, "path"))}",
            "create_directory" => $"Creating folder {FileName(GetStr(root, "path"))}",
            "delete_file" => $"Deleting {FileName(GetStr(root, "path"))}",
            "move_file" => $"Moving {FileName(GetStr(root, "from"))} \u2192 {FileName(GetStr(root, "to"))}",
            "search_files" => $"Searching for \u2018{GetStr(root, "pattern")}\u2019 in {FileName(GetStr(root, "directory"))}/",
            "execute_command" => DescribeCommand(GetStr(root, "command")),
            "read_command_output" => "Reading command output",
            "open_url" => $"Fetching {GetStr(root, "url")}",
            "web_fetch" => $"Fetching {GetStr(root, "url")}",
            "open_browser_tab" => $"Opening {GetStr(root, "url")}",
            "list_browser_tabs" => "Listing open browser tabs",
            "read_browser_tab" => "Reading current browser tab",
            "new_task" => $"Starting task: {Truncate(GetStr(root, "task"), 60)}",
            "attempt_completion" => "Finishing up",
            "ask_followup_question" => $"Asking: {Truncate(GetStr(root, "question"), 60)}",
            "skill" => $"Running skill: {GetStr(root, "name")}",
            "switch_model" => $"Switching to {GetStr(root, "model_name")}",
            "switch_mode" => $"Switching to {GetStr(root, "mode")} mode",
            "update_todo_list" => "Updating to-do list",
            "begin_keyboard_session" => "Starting keyboard session",
            "end_keyboard_session" => "Ending keyboard session",
            "type_text" => $"Typing \u201C{Truncate(GetStr(root, "text"), 40)}\u201D",
            "key_press" => $"Pressing {GetStr(root, "key")}",
            "key_combo" => $"Pressing {GetStr(root, "keys")}",
            "begin_mouse_session" => $"Starting mouse session ({GetStr(root, "duration_minutes")} min)",
            "end_mouse_session" => "Ending mouse session",
            "take_screenshot" => "Taking a screenshot",
            "mouse_move" => $"Moving mouse to ({GetStr(root, "x")}, {GetStr(root, "y")})",
            "mouse_click" => $"Clicking at ({GetStr(root, "x")}, {GetStr(root, "y")})",
            "mouse_double_click" => $"Double-clicking at ({GetStr(root, "x")}, {GetStr(root, "y")})",
            "mouse_drag" => $"Dragging ({GetStr(root, "from_x")},{GetStr(root, "from_y")}) \u2192 ({GetStr(root, "to_x")},{GetStr(root, "to_y")})",
            _ => $"Running \u2018{tool}\u2019"
        };

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
                return "Running a command";

            var trimmed = cmd.Trim();
            if (!trimmed.Contains(' ') && !trimmed.Contains('&') && !trimmed.Contains('|'))
            {
                var appName = System.IO.Path.GetFileNameWithoutExtension(trimmed);
                return $"Opening {Capitalize(appName)}";
            }

            return $"Running: {Truncate(trimmed, 60)}";
        }

        private static string Capitalize(string s) =>
            string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s[1..];
    }
}
