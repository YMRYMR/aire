using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using Aire.Services;
using ChatMessage = Aire.UI.MainWindow.Models.ChatMessage;
namespace Aire
{
    public partial class MainWindow
    {
        private ChatMessage CreateDateSeparator(DateTime date)
        {
            return new ChatMessage
            {
                Sender = "Date",
                Text = FormatDate(date),
                Timestamp = string.Empty,
                BackgroundBrush = System.Windows.Media.Brushes.Transparent,
                SenderForeground = SystemFgBrush
            };
        }

        private static string FormatDate(DateTime date)
        {
            var today = DateTime.Today;
            if (date.Date == today)
                return "Today";
            if (date.Date == today.AddDays(-1))
                return "Yesterday";
            if ((today - date.Date).TotalDays < 7)
                return date.ToString("dddd");
            if (date.Year == today.Year)
                return date.ToString("MMMM d");
            return date.ToString("MMMM d, yyyy");
        }

        private void AddToUI(ChatMessage msg)
        {
            if (msg.Sender != "System" && msg.Sender != "Date" && msg.MessageDate.HasValue)
            {
                var lastDate = Messages.LastOrDefault(m => m.MessageDate.HasValue)?.MessageDate;
                if (lastDate == null || lastDate.Value.Date != msg.MessageDate.Value.Date)
                    Messages.Add(CreateDateSeparator(msg.MessageDate.Value));
            }

            Messages.Add(msg);
        }

        private void RemoveFromUI(ChatMessage msg)
        {
            Messages.Remove(msg);
        }

        private async Task AddSystemMessageAsync(string text, bool persistToConversation = true)
        {
            Messages.Add(new ChatMessage
            {
                Sender = "System",
                Text = text,
                Timestamp = DateTime.Now.ToString("HH:mm"),
                BackgroundBrush = SystemBgBrush,
                SenderForeground = SystemFgBrush
            });

            if (persistToConversation && _currentConversationId.HasValue)
                await _chatSessionApplicationService.PersistSystemMessageAsync(_currentConversationId.Value, text);
        }

        private async Task AddOrchestratorNarrativeAsync(string text, bool persistToConversation = true)
        {
            var orchestratorBrush = TryFindResource("OrchestratorMessageBrush") as System.Windows.Media.Brush ?? SystemBgBrush;
            var orchestratorTextBrush = TryFindResource("OrchestratorMessageTextBrush") as System.Windows.Media.Brush ?? SystemFgBrush;

            Messages.Add(new ChatMessage
            {
                Sender = "Orchestrator",
                Text = text,
                Timestamp = DateTime.Now.ToString("HH:mm"),
                BackgroundBrush = orchestratorBrush,
                SenderForeground = orchestratorTextBrush,
                IsOrchestratorNarrative = true
            });

            if (persistToConversation && _currentConversationId.HasValue)
                await _chatSessionApplicationService.PersistOrchestratorMessageAsync(_currentConversationId.Value, text);
        }

        private Task AddOrchestratorToolNarrativeAsync(ToolCallRequest request, bool persistToConversation = true)
        {
            if (_agentModeService?.IsActive != true)
                return Task.CompletedTask;

            var text = BuildOrchestratorToolNarrative(request);
            if (string.IsNullOrWhiteSpace(text))
                return Task.CompletedTask;

            return AddOrchestratorNarrativeAsync(text, persistToConversation);
        }

        private string BuildOrchestratorToolNarrative(ToolCallRequest request)
        {
            string GetParam(string key)
            {
                if (request.Parameters.ValueKind == JsonValueKind.Object &&
                    request.Parameters.TryGetProperty(key, out var value))
                {
                    return value.ValueKind switch
                    {
                        JsonValueKind.String => value.GetString() ?? string.Empty,
                        JsonValueKind.Number => value.GetRawText(),
                        JsonValueKind.True or JsonValueKind.False => value.GetBoolean().ToString(),
                        JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
                        _ => value.GetRawText()
                    };
                }

                return string.Empty;
            }

            string Quote(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : $"'{value}'";

            var path = GetParam("path");
            var directory = GetParam("directory");
            var from = GetParam("from");
            var to = GetParam("to");
            var url = GetParam("url");
            var query = GetParam("query");
            var command = GetParam("command");
            var text = GetParam("text");
            var title = GetParam("title");
            var key = GetParam("key");
            var question = GetParam("question");

            return request.Tool switch
            {
                "read_file" or "open_file" =>
                    $"I’m going to read the contents of {Quote(path)}.",
                "list_directory" or "list_files" =>
                    $"I’m going to look through {Quote(path)}.",
                "search_files" =>
                    $"I’m going to search {Quote(query)} in {Quote(directory)}.",
                "write_file" or "write_to_file" =>
                    $"I’m going to write to {Quote(path)}.",
                "create_directory" =>
                    $"I’m going to create the folder {Quote(path)}.",
                "delete_file" =>
                    $"I’m going to delete {Quote(path)}.",
                "move_file" =>
                    $"I’m going to move {Quote(from)} to {Quote(to)}.",
                "execute_command" =>
                    string.IsNullOrWhiteSpace(command)
                        ? "I’m going to run a command to continue the task."
                        : $"I’m going to run a command to continue the task.",
                "read_command_output" =>
                    "I’m going to read the result of the command.",
                "read_browser_tab" =>
                    "I’m going to read the current browser tab.",
                "take_screenshot" =>
                    "I’m going to save a screenshot of the screen.",
                "open_url" or "open_browser_tab" or "http_request" or "get_browser_html" or "execute_browser_script" =>
                    request.Tool == "execute_browser_script"
                        ? string.IsNullOrWhiteSpace(url)
                            ? "I’m going to run a browser script to continue the task."
                            : $"I’m going to run a browser script on {Quote(url)}."
                        : string.IsNullOrWhiteSpace(url)
                            ? "I’m going to check a webpage."
                            : $"I’m going to check the webpage {Quote(url)}.",
                "switch_browser_tab" =>
                    "I’m going to switch to another browser tab.",
                "close_browser_tab" =>
                    "I’m going to close a browser tab.",
                "get_browser_cookies" =>
                    "I’m going to read the browser cookies.",
                "get_system_info" =>
                    "I’m going to check this computer’s system information.",
                "get_running_processes" =>
                    "I’m going to look at the running processes.",
                "get_active_window" =>
                    "I’m going to check which window is active.",
                "get_selected_text" =>
                    "I’m going to read the currently selected text.",
                "get_clipboard" =>
                    "I’m going to read the clipboard.",
                "set_clipboard" =>
                    "I’m going to copy the result to the clipboard.",
                "show_notification" =>
                    string.IsNullOrWhiteSpace(title)
                        ? "I’m going to show a notification."
                        : $"I’m going to show a notification titled {Quote(title)}.",
                "send_email" =>
                    "I’m going to send an email.",
                "read_emails" =>
                    "I’m going to read the emails.",
                "search_emails" =>
                    string.IsNullOrWhiteSpace(query)
                        ? "I’m going to search the emails."
                        : $"I’m going to search the emails for {Quote(query)}.",
                "reply_to_email" =>
                    "I’m going to reply to an email.",
                "remember" =>
                    string.IsNullOrWhiteSpace(key)
                        ? "I’m going to remember this for later."
                        : $"I’m going to remember {Quote(key)} for later.",
                "recall" =>
                    string.IsNullOrWhiteSpace(key)
                        ? "I’m going to look up something I remembered before."
                        : $"I’m going to look up {Quote(key)}.",
                "set_reminder" =>
                    "I’m going to set a reminder.",
                "ask_followup_question" =>
                    string.IsNullOrWhiteSpace(question)
                        ? "I need to ask a follow-up question."
                        : $"I need to ask a follow-up question: {question}",
                "update_todo_list" =>
                    "I’m going to update the task list.",
                "skill" =>
                    "I’m going to run a skill to continue the task.",
                "request_context" =>
                    "I’m going to ask for more context.",
                "attempt_completion" =>
                    "I’m going to finish up the task.",
                "mouse_move" or "mouse_click" or "mouse_double_click" or "mouse_drag" or "mouse_scroll" =>
                    "I’m going to interact with the screen.",
                "type_text" =>
                    string.IsNullOrWhiteSpace(text)
                        ? "I’m going to type some text."
                        : $"I’m going to type {Quote(text)}.",
                "key_press" =>
                    "I’m going to press a key.",
                var t when t.StartsWith("mcp:", StringComparison.OrdinalIgnoreCase) ||
                            t.StartsWith("mcp_", StringComparison.OrdinalIgnoreCase) ||
                            t.Contains(':') =>
                    "I’m going to use an external tool.",
                _ =>
                    "I’m going to use a tool to continue working on the goal."
            };
        }

        private async Task AddErrorMessageAsync(string rawError, string? cooldownMsg = null)
        {
            var display = Aire.Services.ProviderErrorClassifier.ExtractReadableMessage(rawError) ?? rawError;
            var text = cooldownMsg != null ? $"{display}\n\n{cooldownMsg}" : display;
            Messages.Add(new ChatMessage
            {
                Sender = "System",
                Text = text,
                Timestamp = DateTime.Now.ToString("HH:mm"),
                BackgroundBrush = ErrorBgBrush,
                SenderForeground = ErrorFgBrush
            });

            if (_currentConversationId.HasValue)
                await _chatSessionApplicationService.PersistSystemMessageAsync(_currentConversationId.Value, text);
        }

        private void AddErrorMessage(string rawError, string? cooldownMsg = null)
            => _ = AddErrorMessageAsync(rawError, cooldownMsg);

        private void ScrollToBottom(bool force = false)
        {
            if (!force && !_followMessagesScroll)
                return;

            Dispatcher.BeginInvoke(
                () => MessagesScrollViewer.ScrollToBottom(),
                System.Windows.Threading.DispatcherPriority.Background);
        }

        private void ScrollMessagesPageUp()
        {
            Dispatcher.BeginInvoke(() =>
            {
                var step = Math.Max(MessagesScrollViewer.ViewportHeight, 50.0);
                var offset = Math.Max(0.0, MessagesScrollViewer.VerticalOffset - step);
                MessagesScrollViewer.ScrollToVerticalOffset(offset);
            }, System.Windows.Threading.DispatcherPriority.Background);
        }

        private void ScrollMessagesPageDown()
        {
            Dispatcher.BeginInvoke(() =>
            {
                var step = Math.Max(MessagesScrollViewer.ViewportHeight, 50.0);
                var offset = Math.Min(MessagesScrollViewer.ScrollableHeight,
                    MessagesScrollViewer.VerticalOffset + step);
                MessagesScrollViewer.ScrollToVerticalOffset(offset);
            }, System.Windows.Threading.DispatcherPriority.Background);
        }

        private void ScrollMessagesToTop()
        {
            Dispatcher.BeginInvoke(
                () => MessagesScrollViewer.ScrollToTop(),
                System.Windows.Threading.DispatcherPriority.Background);
        }

        private void ScrollMessagesToBottom()
        {
            ScrollToBottom(force: true);
        }

        private void MessagesScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (MessagesScrollViewer == null)
                return;

            var threshold = 2.0;
            var atBottom = MessagesScrollViewer.VerticalOffset + MessagesScrollViewer.ViewportHeight >=
                           MessagesScrollViewer.ExtentHeight - threshold;
            _followMessagesScroll = atBottom;
        }
    }
}
