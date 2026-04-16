namespace Aire.Services
{
    /// <summary>
    /// Shared tool-name metadata used by both app logic and future cross-platform tool hosts.
    /// </summary>
    public static class ToolExecutionMetadata
    {
        private static readonly HashSet<string> KeyboardTools = new(StringComparer.OrdinalIgnoreCase)
        {
            "begin_keyboard_session",
            "end_keyboard_session",
            "type_text",
            "key_press",
            "key_combo"
        };

        private static readonly HashSet<string> MouseOnlyTools = new(StringComparer.OrdinalIgnoreCase)
        {
            "begin_mouse_session",
            "end_mouse_session",
            "take_screenshot",
            "mouse_move",
            "mouse_click",
            "mouse_double_click",
            "mouse_drag",
            "mouse_scroll"
        };

        public static bool IsKeyboardTool(string tool) => KeyboardTools.Contains(tool);
        public static bool IsMouseTool(string tool) => MouseOnlyTools.Contains(tool);
        public static bool IsSessionTool(string tool) => IsKeyboardTool(tool) || IsMouseTool(tool);

        /// <summary>Maps common AI-hallucinated or alternate tool names to their canonical equivalents.</summary>
        public static string NormalizeToolName(string tool) => tool switch
        {
            // ── File system ───────────────────────────────────────────────────
            "ls" or "list_dir" or "listdir" or "dir" or
            "list_files" or "ls_dir" or "list_folder" => "list_directory",

            "cat" or "file_read" or "get_file" or "get_file_content" or
            "read_text" or "read_file_content" or "file_contents" => "read_file",

            "file_write" or "save_file" or "write_to_file" or
            "write_text" or "create_file" => "write_file",

            "diff" or "patch" or "edit_file" or "file_patch" or "file_edit" => "apply_diff",

            "find" or "glob" or "file_search" or "find_file" or
            "find_files" or "locate_files" or "search_file" => "search_files",

            "grep" or "grep_files" or "search_in_files" or
            "find_in_files" or "search_content" => "search_file_content",

            "mkdir" or "make_dir" or "make_directory" or
            "create_dir" or "new_directory" => "create_directory",

            "rm" or "remove_file" or "file_delete" or
            "del" or "delete" => "delete_file",

            "mv" or "rename_file" or "file_move" or "rename" => "move_file",

            "bash" or "shell" or "run_command" or "run_cmd" or
            "terminal" or "cmd" or "sh" or "exec" or
            "execute" or "run_shell" => "execute_command",

            "get_output" or "cmd_output" or "command_output" or "read_output" => "read_command_output",

            // ── Web / HTTP ────────────────────────────────────────────────────
            "browse" or "open_browser" or "navigate_to" or "goto" or
            "visit" or "web_browse" or "web_open" or "navigate" or
            "open_webpage" or "open_link" => "open_url",

            "http_get" or "http_post" or "curl" or "api_call" or
            "web_request" or "make_request" or "api_request" => "http_request",

            // ── Browser tabs ──────────────────────────────────────────────────
            "read_webbrowser_tab" or "read_webbrowser_tabs" or "read_browser_tabs" or
            "read_current_tab" or "browser_read_tab" or "read_tab" => "read_browser_tab",

            "list_webbrowser_tabs" or "list_web_browser_tabs" or "list_tab" or
            "get_browser_tabs" or "tabs" or "browser_list_tabs" or
            "list_open_tabs" => "list_browser_tabs",

            "open_webbrowser_tab" or "open_web_browser_tab" or "open_tab" or
            "browser_open_tab" or "new_tab" or "new_browser_tab" => "open_browser_tab",

            "run_browser_script" or "browser_script" or "browser_execute" or
            "js" or "run_js" or "execute_js" or "eval_js" or "run_script" => "execute_browser_script",

            "browser_html" or "get_html" or "page_html" or
            "page_source" or "page_content" or "get_page_html" => "get_browser_html",

            "browser_cookies" or "get_cookies" or "cookies" or "all_cookies" => "get_browser_cookies",

            "close_tab" or "browser_close_tab" => "close_browser_tab",

            "switch_tab" or "browser_switch_tab" or
            "select_tab" or "activate_tab" or "focus_tab" => "switch_browser_tab",

            // ── Clipboard / notifications / OS ────────────────────────────────
            "clipboard" or "read_clipboard" or "get_paste" => "get_clipboard",
            "write_clipboard" or "copy_to_clipboard" or "copy" => "set_clipboard",

            "notify" or "notification" or "desktop_notification" or
            "send_notification" or "alert" or "toast" => "show_notification",

            "system_info" or "sysinfo" or "os_info" or "uname" => "get_system_info",

            "processes" or "list_processes" or "ps" or
            "running_processes" or "get_processes" => "get_running_processes",

            "active_window" or "foreground_window" or
            "current_window" or "focused_window" => "get_active_window",

            "selected_text" or "get_selection" or
            "selection" or "current_selection" => "get_selected_text",

            "launch" or "open_app" or "open_application" or "run_app" => "open_file",

            // ── Memory / reminders ────────────────────────────────────────────
            "save_memory" or "memorize" or "store_memory" or "mem_set" => "remember",
            "load_memory" or "mem_get" or "retrieve_memory" or "get_memory" => "recall",
            "remind" or "add_reminder" or "create_reminder" or "schedule_reminder" => "set_reminder",

            // ── Agent / workflow ──────────────────────────────────────────────
            "task" or "subtask" or "create_task" or "new_subtask" => "new_task",

            "ask" or "question" or "clarify" or "followup" or
            "ask_question" or "ask_user" or "request_info" => "ask_followup_question",

            "done" or "finish" or "final_answer" or
            "answer" or "completion" or "complete" => "attempt_completion",

            "run_skill" or "use_skill" or "call_skill" => "skill",

            "mode" or "change_mode" or "set_mode" => "switch_mode",

            "todo" or "update_todo" or "set_todo" or
            "todo_list" or "manage_todo" => "update_todo_list",

            "display_image" or "show_picture" or "image" or
            "render_image" or "show_img" or "display_img" => "show_image",

            _ => tool
        };
    }
}
