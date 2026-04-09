using System;
using System.Threading;
using System.Threading.Tasks;
using ChatMessage = Aire.UI.MainWindow.Models.ChatMessage;

namespace Aire
{
    public partial class MainWindow
    {
        private sealed partial class ChatCoordinator
        {
            private async Task RunImageGenerationTurnAsync(
                string prompt,
                Providers.IImageGenerationProvider provider,
                bool wasVoice,
                CancellationToken cancellationToken)
            {
                _owner.IsThinking = true;
                try
                {
                    var generated = await _owner._generatedImageApplicationService.GenerateAsync(
                        provider,
                        prompt,
                        _owner._currentConversationId,
                        cancellationToken);

                    _owner._conversationHistory.Add(generated.AssistantHistoryMessage);

                    var now = DateTime.Now;
                    _owner.AddToUI(new ChatMessage
                    {
                        Sender = "AI",
                        Text = MainWindow.AppendImageFallbackLinks(generated.FinalText, new[] { generated.ImagePath }),
                        Timestamp = now.ToString("HH:mm"),
                        MessageDate = now,
                        BackgroundBrush = MainWindow.AiBgBrush,
                        SenderForeground = MainWindow.AiFgBrush,
                        InlineImages = MainWindow.LoadChatImageSources(new[] { generated.ImagePath })
                    });

                    _owner.SpeakResponseIfNeeded(generated.FinalText, wasVoice);
                }
                catch (OperationCanceledException)
                {
                    await _owner.AddSystemMessageAsync("Image generation stopped.");
                }
                catch
                {
                    await _owner.AddErrorMessageAsync("Image generation failed.");
                }
            }
        }
    }
}
