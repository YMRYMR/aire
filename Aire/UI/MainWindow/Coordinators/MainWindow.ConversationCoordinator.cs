using System;
using System.IO;
using System.Threading.Tasks;
using Aire.AppLayer.Chat;
using Aire.Data;
using Aire.Services;
using ChatMessage = Aire.UI.MainWindow.Models.ChatMessage;
using ProviderChatMessage = Aire.Providers.ChatMessage;

namespace Aire
{
    public partial class MainWindow
    {
        private ConversationCoordinator? _conversationCoordinator;
        private ConversationCoordinator ConversationFlow => _conversationCoordinator ??= new ConversationCoordinator(this);

        private sealed class ConversationCoordinator
        {
            private readonly MainWindow _owner;

            public ConversationCoordinator(MainWindow owner)
            {
                _owner = owner;
            }

            public async Task LoadConversationMessagesAsync(int conversationId)
            {
                var messages = await _owner._conversationApplicationService.GetMessagesAsync(conversationId);
                var transcriptService = _owner._conversationTranscriptApplicationService ?? new ConversationTranscriptApplicationService();
                var transcript = transcriptService.BuildTranscript(messages);
                _owner.Messages.Clear();
                _owner._conversationHistory.Clear();
                _owner._inputHistory.Clear();
                _owner._historyIndex = -1;
                _owner._inputDraft = string.Empty;

                foreach (var historyMessage in transcript.ConversationHistory)
                    _owner._conversationHistory.Add(historyMessage);

                foreach (var input in transcript.InputHistory)
                    _owner._inputHistory.Add(input);

                foreach (var entry in transcript.Entries)
                {
                    var bgBrush = entry.Role switch
                    {
                        ConversationTranscriptApplicationService.TranscriptRole.User => MainWindow.UserBgBrush,
                        ConversationTranscriptApplicationService.TranscriptRole.Assistant => MainWindow.AiBgBrush,
                        ConversationTranscriptApplicationService.TranscriptRole.Tool => MainWindow.AiBgBrush,
                        _ => MainWindow.SystemBgBrush
                    };
                    var fgBrush = entry.Role switch
                    {
                        ConversationTranscriptApplicationService.TranscriptRole.User => MainWindow.UserFgBrush,
                        ConversationTranscriptApplicationService.TranscriptRole.Assistant => MainWindow.AiFgBrush,
                        ConversationTranscriptApplicationService.TranscriptRole.Tool => MainWindow.AiFgBrush,
                        _ => MainWindow.SystemFgBrush
                    };

                    if (entry.StartsNewDateSection)
                        _owner.Messages.Add(_owner.CreateDateSeparator(entry.CreatedAt));

                    if (entry.Role == ConversationTranscriptApplicationService.TranscriptRole.Tool)
                    {
                        _owner.Messages.Add(new ChatMessage
                        {
                            Sender = "AI",
                            Text = string.Empty,
                            ToolCallStatus = entry.Text,
                            Timestamp = entry.CreatedAt.ToString("HH:mm"),
                            MessageDate = entry.CreatedAt,
                            BackgroundBrush = bgBrush,
                            SenderForeground = fgBrush,
                        });
                        continue;
                    }

                    var chatMsg = new ChatMessage
                    {
                        Sender = entry.Sender,
                        Text = entry.Text,
                        Timestamp = entry.CreatedAt.ToString("HH:mm"),
                        MessageDate = entry.CreatedAt,
                        BackgroundBrush = bgBrush,
                        SenderForeground = fgBrush
                    };

                    if (entry.ImagePath != null && File.Exists(entry.ImagePath))
                    {
                        try
                        {
                            var bitmap = new System.Windows.Media.Imaging.BitmapImage(new Uri(entry.ImagePath));
                            bitmap.Freeze();
                            if (entry.Role == ConversationTranscriptApplicationService.TranscriptRole.User)
                                chatMsg.AttachedImage = bitmap;
                            else
                                chatMsg.ScreenshotImage = bitmap;
                        }
                        catch (Exception ex)
                        {
                            AppLogger.Warn("ConversationCoordinator.LoadHistory", "Failed to load image for history message", ex);
                        }
                    }

                    _owner.Messages.Add(chatMsg);
                }

                if (_owner.Messages.Count == 0)
                    _owner.LoadWelcomeMessage();
            }

            public async Task ClearConversationAsync()
            {
                if (_owner._currentConversationId.HasValue)
                {
                    var screenshotFolder = Path.Combine(MainWindow.GetScreenshotsFolder(), _owner._currentConversationId.Value.ToString());
                    try { Directory.Delete(screenshotFolder, recursive: true); } catch { }

                try
                {
                        await _owner._conversationApplicationService.DeleteConversationAsync(_owner._currentConversationId.Value);
                        _owner._currentConversationId = null;
                }
                    catch (Exception ex)
                    {
                        UI.ConfirmationDialog.ShowAlert(_owner, "Error", $"Failed to delete conversation: {ex.Message}");
                        return;
                    }
                }

                _owner.Messages.Clear();
                _owner._conversationHistory.Clear();
                _owner._inputHistory.Clear();
                _owner._historyIndex = -1;
                _owner._inputDraft = string.Empty;
                _owner._todoListMessage = null;
                _owner.CloseSearch();
                _owner.LoadWelcomeMessage();
            }

            public async Task<int> CreateConversationAsync(Provider provider, string title, string systemMessage)
            {
                var id = await _owner._conversationApplicationService.CreateConversationAsync(provider.Id, title);
                _owner._currentConversationId = id;
                _owner._conversationHistory.Clear();
                _owner.Messages.Clear();
                _owner.AddToUI(new ChatMessage
                {
                    Sender = "System",
                    Text = systemMessage,
                    SenderForeground = MainWindow.SystemFgBrush
                });
                return id;
            }
        }
    }
}
