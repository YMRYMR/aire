using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Aire.AppLayer.Chat;
using Aire.Services;
using Providers = Aire.Providers;
using ChatMessage = Aire.UI.MainWindow.Models.ChatMessage;

namespace Aire
{
    public partial class MainWindow
    {
        private sealed partial class ChatCoordinator
        {
            private readonly StreamingResponsePresentationService _streamingPresentation = new();

            private async Task<(Providers.AiResponse Response, ChatMessage? StreamedMessage)> RequestProviderResponseAsync(
                System.Collections.Generic.IEnumerable<Providers.ChatMessage> messages,
                CancellationToken cancellationToken)
            {
                if (_owner._currentProvider?.Has(Providers.ProviderCapabilities.Streaming) != true)
                    return (await _owner._chatService.SendMessageWithHistoryAsync(messages, cancellationToken), null);

                ChatMessage? streamedMessage = null;
                // StringBuilder avoids O(n²) string allocation on every chunk for long responses.
                var rawContent = new System.Text.StringBuilder();
                var contentGate = new object();
                var updateScheduled = false;
                void OnChunk(object? _, string chunk)
                {
                    // OnChunk now fires from the thread pool (ChatOrchestrator uses
                    // ConfigureAwait(false) on its await foreach).  The lock is still required
                    // because chunks can arrive from different thread-pool threads.
                    bool shouldSchedule;
                    lock (contentGate)
                    {
                        rawContent.Append(chunk);
                        shouldSchedule = !updateScheduled;
                        if (shouldSchedule)
                            updateScheduled = true;
                    }

                    if (!shouldSchedule)
                        return;

                    // Render priority (7) — runs as part of WPF's render cycle, well ahead of
                    // Background (4), so each chunk appears on screen promptly.
                    _ = _owner.Dispatcher.InvokeAsync(() =>
                    {
                        string rawSnapshot;
                        lock (contentGate)
                        {
                            rawSnapshot = rawContent.ToString();
                            updateScheduled = false;
                        }

                        var visibleText = _streamingPresentation.GetStreamingPreviewText(rawSnapshot);
                        var hasVisibleText = !string.IsNullOrWhiteSpace(visibleText);

                        if (streamedMessage == null && hasVisibleText)
                        {
                            // First visible token — dismiss the "Thinking…" overlay immediately
                            // so the user sees the response start rather than a stuck spinner.
                            _owner.IsThinking = false;

                            streamedMessage = new ChatMessage
                            {
                                Sender = "AI",
                                Text = visibleText,
                                Timestamp = DateTime.Now.ToString("HH:mm"),
                                MessageDate = DateTime.Now,
                                BackgroundBrush = MainWindow.AiBgBrush,
                                SenderForeground = MainWindow.AiFgBrush,
                            };
                            _owner.AddToUI(streamedMessage);
                        }
                        else if (streamedMessage != null)
                        {
                            streamedMessage.Text = visibleText;
                        }

                        if (streamedMessage != null && hasVisibleText && !_owner.Messages.Contains(streamedMessage))
                            _owner.AddToUI(streamedMessage);

                        if (streamedMessage != null && hasVisibleText)
                            _owner.ScrollToBottom();
                    }, DispatcherPriority.Render);
                }

                _owner._chatService.ResponseChunkReceived += OnChunk;
                try
                {
                    var response = await _owner._chatService.StreamMessageWithHistoryAsync(messages, cancellationToken);
                    if (ShouldFallbackToNonStreaming(response, streamedMessage))
                        return (await _owner._chatService.SendMessageWithHistoryAsync(messages, cancellationToken), null);

                    if (string.IsNullOrWhiteSpace(response.Content) &&
                        streamedMessage != null &&
                        !string.IsNullOrWhiteSpace(streamedMessage.Text))
                    {
                        response.Content = streamedMessage.Text;
                    }

                    return (response, streamedMessage);
                }
                finally
                {
                    _owner._chatService.ResponseChunkReceived -= OnChunk;
                }
            }

            private static bool ShouldFallbackToNonStreaming(Providers.AiResponse response, ChatMessage? streamedMessage)
            {
                if (!response.IsSuccess)
                    return false;

                if (!string.IsNullOrWhiteSpace(response.Content))
                    return false;

                if (streamedMessage != null && !string.IsNullOrWhiteSpace(streamedMessage.Text))
                    return false;

                var parsed = ToolCallParser.Parse(response.Content ?? string.Empty);
                return !parsed.HasToolCall && string.IsNullOrWhiteSpace(parsed.TextContent);
            }
        }
    }
}
