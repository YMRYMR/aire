using System.Linq;
using System.Windows;
using ChatMessage = Aire.UI.MainWindow.Models.ChatMessage;

namespace Aire
{
    public partial class MainWindow
    {
        private ChatMessage? GetPendingApproval() =>
            _messages.LastOrDefault(m => m.IsApprovalPending && m.ApprovalTcs != null && !m.ApprovalTcs.Task.IsCompleted);

        private void ApproveToolCall_Click(object sender, RoutedEventArgs e)
        {
            var msg = (ChatMessage)((System.Windows.Controls.Button)sender).Tag;
            msg.ApprovalTcs?.TrySetResult(true);
        }

        private void DenyToolCall_Click(object sender, RoutedEventArgs e)
        {
            var msg = (ChatMessage)((System.Windows.Controls.Button)sender).Tag;
            msg.ApprovalTcs?.TrySetResult(false);
        }
    }
}
