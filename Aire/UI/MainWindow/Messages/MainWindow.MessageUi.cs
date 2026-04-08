using System;
using System.Linq;
using System.Threading.Tasks;
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

        private void ScrollToBottom()
        {
            Dispatcher.BeginInvoke(
                () => MessagesScrollViewer.ScrollToBottom(),
                System.Windows.Threading.DispatcherPriority.Background);
        }
    }
}
