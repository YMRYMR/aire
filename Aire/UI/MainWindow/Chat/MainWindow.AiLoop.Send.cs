using System.Threading.Tasks;
using System.Windows.Threading;

namespace Aire
{
    public partial class MainWindow
    {
        private async Task SendMessageAsync()
            => await ChatFlow.SendMessageAsync();

        private void QueueSendMessage()
        {
            if (_isProcessing || string.IsNullOrWhiteSpace(InputTextBox.Text))
                return;

            IsThinking = true;
            Dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new System.Action(() => _ = SendMessageAsync()));
        }
    }
}
