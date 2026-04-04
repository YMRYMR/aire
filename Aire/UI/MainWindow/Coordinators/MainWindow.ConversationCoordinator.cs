using System;
using System.IO;
using System.Linq;
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

                    if (entry.ImageReferences.Count > 0)
                    {
                        try
                        {
                            var bitmaps = MainWindow.LoadChatImageSources(entry.ImageReferences);
                            if (bitmaps != null)
                            {
                                if (entry.Role == ConversationTranscriptApplicationService.TranscriptRole.User)
                                    chatMsg.AttachedImage = bitmaps[0];
                                else
                                    chatMsg.InlineImages = bitmaps;
                            }
                            else
                            {
                                chatMsg.Text = MainWindow.AppendImageFallbackLinks(chatMsg.Text, entry.ImageReferences);
                            }
                        }
                        catch
                        {
                            AppLogger.Warn("ConversationCoordinator.LoadHistory", "Failed to load images for history message");
                            chatMsg.Text = MainWindow.AppendImageFallbackLinks(chatMsg.Text, entry.ImageReferences);
                        }
                    }

                    if (entry.FileAttachments.Count > 0)
                    {
                        chatMsg.FileAttachments = new System.Collections.ObjectModel.ObservableCollection<MessageAttachment>(entry.FileAttachments);
                    }

                    _owner.Messages.Add(chatMsg);
                }

                if (_owner.Messages.Count == 0)
                    _owner.LoadWelcomeMessage();

                _owner.ScrollToBottom();
            }

            public async Task SyncConversationSelectionStateAsync(int conversationId)
            {
                var conversation = await _owner._conversationApplicationService.GetConversationAsync(conversationId);
                if (conversation == null)
                    return;

                _owner.ApplyAssistantModeState(conversation.AssistantModeKey);

                var provider = (_owner._localApiApplicationService ?? new Aire.AppLayer.Api.LocalApiApplicationService())
                    .ResolveConversationProvider(conversation, _owner.ProviderComboBox.Items.OfType<Provider>());
                if (provider == null || provider.Id == _owner._currentProviderId)
                    return;

                _owner._suppressProviderChange = true;
                _owner.ProviderComboBox.SelectedItem = provider;
                _owner._suppressProviderChange = false;
                _owner._currentProviderId = provider.Id;

                try { _owner._currentProvider = _owner._providerFactory.CreateProvider(provider); }
                catch { _owner._currentProvider = null; }

                await _owner._chatService.SetProviderAsync(provider.Id);
                await _owner._chatSessionApplicationService.SaveSelectedProviderAsync(provider.Id);
                _owner.UpdateCapabilityUI();
                _owner.StartTokenUsageRefreshTimer();
            }

            public async Task ClearConversationAsync()
            {
                if (_owner._currentConversationId.HasValue)
                {
                    var deleted = await DeleteConversationAsync(_owner._currentConversationId.Value);
                    if (!deleted)
                        return;
                }

                ResetConversationUiState();
            }

            public async Task<bool> DeleteConversationAsync(int conversationId)
            {
                var screenshotFolder = Path.Combine(MainWindow.GetScreenshotsFolder(), conversationId.ToString());
                try { Directory.Delete(screenshotFolder, recursive: true); } catch { }

                try
                {
                    await _owner._conversationApplicationService.DeleteConversationAsync(conversationId);
                }
                catch
                {
                    UI.ConfirmationDialog.ShowAlert(_owner, "Error", "Failed to delete conversation.");
                    return false;
                }

                if (_owner._currentConversationId == conversationId)
                    ResetConversationUiState();

                return true;
            }

            public async Task DeleteAllConversationsAsync()
            {
                await _owner._conversationApplicationService.DeleteAllConversationsAsync();
                ResetConversationUiState();
            }

            public async Task<int> CreateConversationAsync(Provider provider, string title, string systemMessage)
            {
                var id = await _owner._conversationApplicationService.CreateConversationAsync(provider.Id, title);
                await _owner._conversationApplicationService.UpdateConversationAssistantModeAsync(id, _owner._assistantModeKey);
                _owner._currentConversationId = id;
                _owner._conversationHistory.Clear();
                _owner.Messages.Clear();
                _owner.AddSystemMessage(systemMessage);
                return id;
            }

            private void ResetConversationUiState()
            {
                _owner._currentConversationId = null;
                _owner.Messages.Clear();
                _owner._conversationHistory.Clear();
                _owner._inputHistory.Clear();
                _owner._historyIndex = -1;
                _owner._inputDraft = string.Empty;
                _owner._todoListMessage = null;
                _owner.ApplyAssistantModeState(_owner._assistantModeApplicationService.GetDefaultMode().Key);
                _owner.CloseSearch();
                _owner.LoadWelcomeMessage();
            }
        }
    }
}
