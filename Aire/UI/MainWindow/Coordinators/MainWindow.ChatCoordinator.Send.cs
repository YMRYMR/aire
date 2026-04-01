using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Aire.Services.Workflows;
using ChatMessage = Aire.UI.MainWindow.Models.ChatMessage;
using ProviderChatMessage = Aire.Providers.ChatMessage;

namespace Aire
{
    public partial class MainWindow
    {
        private sealed partial class ChatCoordinator
        {
            private readonly ChatSubmissionWorkflowService _submissionWorkflow = new();

            public async Task SendMessageAsync()
            {
                var text = _owner.InputTextBox.Text.Trim();
                if (string.IsNullOrEmpty(text) || _owner._isProcessing)
                    return;

                var wasVoice = _owner.VoiceFlow.ConsumeVoiceOrigin();

                var historyState = _submissionWorkflow.UpdateInputHistory(_owner._inputHistory, text);
                _owner._historyIndex = historyState.HistoryIndex;
                _owner._inputDraft = historyState.Draft;

                var now = DateTime.Now;
                var prepared = _submissionWorkflow.PrepareSubmission(
                    text,
                    _owner._attachedImagePath,
                    _owner._attachedFilePath,
                    MainWindow.TextExts,
                    _owner._conversationHistory.Count);
                var messageContent = prepared.DisplayContent;

                if (_owner._currentConversationId.HasValue)
                {
                    await _owner._chatSessionApplicationService.PersistUserMessageAsync(
                        _owner._currentConversationId.Value,
                        messageContent,
                        _owner._attachedImagePath ?? _owner._attachedFilePath,
                        prepared.SuggestedConversationTitle);
                    if (!string.IsNullOrWhiteSpace(prepared.SuggestedConversationTitle))
                        _ = _owner.RefreshSidebarAsync();
                }

                var attachedImageSource = _owner._attachedImagePath != null ? _owner.AttachedImagePreview.Source : null;

                _owner.AddToUI(new ChatMessage
                {
                    Sender = "You",
                    Text = messageContent,
                    Timestamp = now.ToString("HH:mm"),
                    MessageDate = now,
                    BackgroundBrush = MainWindow.UserBgBrush,
                    SenderForeground = MainWindow.UserFgBrush,
                    AttachedImage = attachedImageSource
                });

                _owner._attachedImagePath = null;
                _owner._attachedFilePath = null;
                _owner._attachedFileName = null;
                _owner.AttachedImagePreview.Source = null;
                _owner.ImagePreviewPanel.Visibility = System.Windows.Visibility.Collapsed;
                _owner.ImageThumbnailBorder.Visibility = System.Windows.Visibility.Collapsed;
                _owner.FileChipBorder.Visibility = System.Windows.Visibility.Collapsed;
                _owner.LargeFileWarning.Visibility = System.Windows.Visibility.Collapsed;
                _owner.InputTextBox.Clear();

                _owner._conversationHistory.Add(
                    _submissionWorkflow.BuildProviderHistoryMessage(
                        prepared.PersistedContent,
                        prepared.HistoryImagePath));

                _owner._isProcessing = true;
                _owner._aiCancellationTokenSource?.Cancel();
                _owner._aiCancellationTokenSource?.Dispose();
                _owner._aiCancellationTokenSource = new CancellationTokenSource();
                var cancellationToken = _owner._aiCancellationTokenSource.Token;
                _owner.ScrollToBottom();

                try
                {
                    await RunAiTurnAsync(wasVoice: wasVoice, cancellationToken: cancellationToken);
                }
                finally
                {
                    _owner.IsThinking = false;
                    _owner._isProcessing = false;
                    _owner._aiCancellationTokenSource?.Dispose();
                    _owner._aiCancellationTokenSource = null;
                    _owner.ScrollToBottom();
                }
            }
        }
    }
}
