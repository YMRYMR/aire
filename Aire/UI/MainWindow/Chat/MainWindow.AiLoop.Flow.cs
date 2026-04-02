using System.Threading.Tasks;
using System.Windows;
using Aire.Services;

namespace Aire
{
    public partial class MainWindow
    {
        private void SendButton_Click(object sender, RoutedEventArgs e)
            => QueueSendMessage();

        public async Task<bool> ApiSendMessageAsync(string text)
        {
            await AppStartupState.WaitUntilReadyAsync();
            if (string.IsNullOrWhiteSpace(text) || _isProcessing)
                return false;

            if (!_currentConversationId.HasValue)
            {
                try
                {
                    await ApiCreateConversationAsync();
                }
                catch
                {
                    return false;
                }
            }

            InputTextBox.Text = text;
            await SendMessageAsync();
            return true;
        }

        private void StopAiButton_Click(object sender, RoutedEventArgs e)
        {
            _aiCancellationTokenSource?.Cancel();
        }
    }
}
