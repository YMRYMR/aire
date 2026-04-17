using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Aire.AppLayer.Chat;
using Aire.Data;
using Aire.Services;
using ChatMessage = Aire.UI.MainWindow.Models.ChatMessage;
using ProviderChatMessage = Aire.Providers.ChatMessage;
using Brush = System.Windows.Media.Brush;

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
                    var orchestratorBg = _owner.TryFindResource("OrchestratorMessageBrush") as Brush;
                    var orchestratorFg = _owner.TryFindResource("OrchestratorMessageTextBrush") as Brush;
                    Brush bgBrush = MainWindow.SystemBgBrush;
                    Brush fgBrush = MainWindow.SystemFgBrush;

                    if (entry.Role == ConversationTranscriptApplicationService.TranscriptRole.User)
                    {
                        bgBrush = MainWindow.UserBgBrush;
                        fgBrush = MainWindow.UserFgBrush;
                    }
                    else if (entry.Role == ConversationTranscriptApplicationService.TranscriptRole.Assistant ||
                             entry.Role == ConversationTranscriptApplicationService.TranscriptRole.Tool)
                    {
                        bgBrush = MainWindow.AiBgBrush;
                        fgBrush = MainWindow.AiFgBrush;
                    }
                    else if (entry.Role == ConversationTranscriptApplicationService.TranscriptRole.Orchestrator)
                    {
                        if (orchestratorBg != null)
                            bgBrush = orchestratorBg;
                        if (orchestratorFg != null)
                            fgBrush = orchestratorFg;
                    }

                    if (entry.StartsNewDateSection)
                        _owner.Messages.Add(_owner.CreateDateSeparator(entry.CreatedAt));

                    if (entry.Role == ConversationTranscriptApplicationService.TranscriptRole.Tool)
                    {
                        string statusText = entry.Text ?? string.Empty;
                        string actionText = string.Empty;
                        if (statusText.Contains('✗'))
                        {
                            var denied = ChatMessage.SplitDeniedStatus(statusText);
                            statusText = denied.StatusText;
                            actionText = denied.ActionText;
                        }
                        _owner.Messages.Add(new ChatMessage
                        {
                            Sender = "AI",
                            Text = string.Empty,
                            ToolCallStatus = statusText,
                            DeniedToolCallActionText = actionText,
                            Timestamp = entry.CreatedAt.ToString("HH:mm"),
                            MessageDate = entry.CreatedAt,
                            BackgroundBrush = bgBrush,
                            SenderForeground = fgBrush,
                        });
                        continue;
                    }

                    var sender = entry.Role == ConversationTranscriptApplicationService.TranscriptRole.Orchestrator
                        ? "Orchestrator"
                        : entry.Sender;

                    var chatMsg = new ChatMessage
                    {
                        DbMessageId = entry.MessageId,
                        Sender = sender,
                        Text = entry.Text,
                        Timestamp = entry.CreatedAt.ToString("HH:mm"),
                        MessageDate = entry.CreatedAt,
                        BackgroundBrush = bgBrush,
                        SenderForeground = fgBrush
                    };

                    if (entry.Role == ConversationTranscriptApplicationService.TranscriptRole.Orchestrator)
                        chatMsg.IsOrchestratorNarrative = true;

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

            public async Task SyncConversationSelectionStateAsync(int conversationId, int? previousConversationId = null)
            {
                var conversation = await _owner._conversationApplicationService.GetConversationAsync(conversationId);
                if (conversation == null)
                    return;

                if (previousConversationId.HasValue &&
                    previousConversationId.Value != conversationId &&
                    _owner._agentModeService?.IsActive == true)
                {
                    await _owner.PauseOrchestratorConversationAsync(previousConversationId.Value, "conversation switched", clearSessionStatus: true);
                }

                _owner.ApplyAssistantModeState(conversation.AssistantModeKey);

                var provider = (_owner._localApiApplicationService ?? new Aire.AppLayer.Api.LocalApiApplicationService())
                    .ResolveConversationProvider(conversation, _owner.ProviderComboBox.Items.OfType<Provider>());
                if (provider == null || provider.Id == _owner._currentProviderId)
                    return;

                try { _owner._currentProvider = _owner._providerFactory.CreateProvider(provider); }
                catch { _owner._currentProvider = null; }

                _owner._suppressProviderChange = true;
                try
                {
                    _owner.ProviderComboBox.SelectedItem = provider;
                    _owner._currentProviderId = provider.Id;

                    await _owner._chatService.SetProviderAsync(provider.Id);
                    await _owner._chatSessionApplicationService.SaveSelectedProviderAsync(provider.Id);
                    _owner.UpdateCapabilityUI();
                    _owner.StartTokenUsageRefreshTimer();
                }
                finally
                {
                _owner._suppressProviderChange = false;
                }
            }

            public async Task ClearConversationAsync()
            {
                if (_owner._currentConversationId.HasValue)
                {
                    await DeleteConversationAsync(_owner._currentConversationId.Value);
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
                {
                    var search = _owner.ConversationSidebar?.SearchText.Trim();
                    var remainingConversations = await _owner._conversationApplicationService.ListConversationsAsync(search);

                    if (remainingConversations.Count > 0)
                    {
                        var nextConversation = remainingConversations.First();
                        _owner._currentConversationId = nextConversation.Id;
                        await SyncConversationSelectionStateAsync(nextConversation.Id);
                        await LoadConversationMessagesAsync(nextConversation.Id);
                        await _owner.RefreshSidebarAsync(search);
                        return true;
                    }

                    ResetConversationUiState();
                }

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
                await _owner.AddSystemMessageAsync(systemMessage);
                return id;
            }

            /// <summary>
            /// Branches the current conversation from a specific message, creating a new
            /// conversation with all messages up to and including that point.
            /// </summary>
            public async Task BranchFromMessageAsync(int upToMessageId)
            {
                if (!_owner._currentConversationId.HasValue) return;

                var conversationId = _owner._currentConversationId.Value;
                var newId = await _owner._databaseService.BranchConversationAsync(conversationId, upToMessageId);

                _owner._currentConversationId = newId;
                await LoadConversationMessagesAsync(newId);
                await _owner.RefreshSidebarAsync();
                await _owner.AddSystemMessageAsync(
                    LocalizationService.S("branch.created", "Branched conversation — messages after the branch point were removed."));
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
