using static Aire.Services.Tools.ToolHelpers;

namespace Aire.Services
{
    /// <summary>
    /// Executes built-in meta-skills that don't depend on platform services.
    /// </summary>
    public static class BuiltinToolSkillService
    {
        public static ToolExecutionResult Execute(ToolCallRequest request)
        {
            var name = GetString(request, "name").Trim().ToLowerInvariant()
                .Replace('-', '_').Replace(' ', '_');

            if (name is "list_tools" or "tools" or "available_tools")
            {
                var text =
                    "AVAILABLE TOOLS:\n" +
                    "File System: list_directory, read_file, write_file, apply_diff, search_files, search_file_content, create_directory, delete_file, move_file.\n" +
                    "Command Execution: execute_command, read_command_output.\n" +
                    "Web (background): open_url(url, max_chars?), http_request(url, method?, headers?, body?).\n" +
                    "Web (browser): open_browser_tab, list_browser_tabs, read_browser_tab, switch_browser_tab, close_browser_tab, get_browser_html, get_browser_cookies, execute_browser_script.\n" +
                    "System Control: take_screenshot, begin_keyboard_session, end_keyboard_session, key_press, key_combo, type_text, begin_mouse_session, end_mouse_session, mouse_move, mouse_click, mouse_double_click, mouse_drag, mouse_scroll.\n" +
                    "System Utilities: show_notification(title, message), get_clipboard(), set_clipboard(text), get_system_info(), get_running_processes(top_n?, filter?), get_active_window(), get_selected_text(), open_file(path).\n" +
                    "Memory: remember(key, value), recall(key?), set_reminder(message, delay_minutes).\n" +
                    "Email: read_emails(account?, count?), search_emails(query, account?), send_email(to, subject, body, account?), reply_to_email(message_id, body, account?).\n" +
                    "Agent / Task flow: new_task(task), attempt_completion(result), ask_followup_question(question), skill(name), switch_mode(mode), switch_model(model_name, reason, direction), update_todo_list(todos), show_image(path_or_url, caption?).";

                return new ToolExecutionResult { TextResult = text };
            }

            return new ToolExecutionResult
            {
                TextResult = $"Unknown built-in skill '{name}'. Use skill(name=\"list_tools\") to see all available tools."
            };
        }
    }
}
