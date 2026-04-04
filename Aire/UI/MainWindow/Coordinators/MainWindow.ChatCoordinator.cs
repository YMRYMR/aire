using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Aire.Services;
using Aire.Services.Workflows;
using ChatMessage = Aire.UI.MainWindow.Models.ChatMessage;
using ProviderChatMessage = Aire.Providers.ChatMessage;

namespace Aire
{
    public partial class MainWindow
    {
        private ChatCoordinator? _chatCoordinator;
        private ChatCoordinator ChatFlow => _chatCoordinator ??= new ChatCoordinator(this);

        /// <summary>
        /// Coordinates one chat turn end-to-end: prompt assembly, provider call, response branching,
        /// and the follow-up tool/result loop. UI updates still happen here, but the branch decisions
        /// are delegated to ChatTurnWorkflowService.
        /// </summary>
        private sealed partial class ChatCoordinator
        {
            private readonly MainWindow _owner;
            private readonly ChatTurnWorkflowService _workflow = new();

            public ChatCoordinator(MainWindow owner)
            {
                _owner = owner;
            }

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
                    _owner.AddSystemMessage("Image generation stopped.");
                }
                catch
                {
                    _owner.AddErrorMessage("Image generation failed.");
                }
            }

            /// <summary>
            /// Runs the current AI turn and continues recursively while the model keeps requesting follow-up actions.
            /// </summary>
            /// <param name="iteration">Current recursion depth used to enforce the tool-call safety limit.</param>
            /// <param name="wasVoice">Whether the originating user message came from the voice pipeline.</param>
            /// <param name="cancellationToken">Cancellation token for the active chat turn.</param>
            public async Task RunAiTurnAsync(int iteration = 0, bool wasVoice = false, CancellationToken cancellationToken = default)
            {
                const int maxIterations = 40;

                if (iteration >= maxIterations)
                {
                    _owner.AddSystemMessage("Maximum tool-call iterations reached — the task has been stopped. You can continue by sending a new message.");
                    return;
                }

                _owner.IsThinking = true;

                var toolsEnabled = _owner.ToolsEnabled;
                _owner._currentProvider?.SetEnabledToolCategories(_owner._enabledToolCategories);
                _owner._currentProvider?.SetToolsEnabled(toolsEnabled);

                // Capture values that require the UI thread before yielding to the thread pool.
                var modelListSection  = _owner.BuildModelListSection();
                var modePromptSection = _owner.BuildAssistantModePrompt();
                var mcpTools          = Services.Mcp.McpManager.Instance.GetAllTools();
                var currentProvider   = _owner._currentProvider;
                var history           = _owner._conversationHistory;
                var contextSettings   = _owner._contextWindowSettings;

                // WindowConversation (TrimConversation) and BuildRequestMessages (prompt string assembly)
                // can be slow on long conversations. Running them on the thread pool lets the
                // DispatcherTimer that drives the IsIndeterminate ProgressBar animation keep firing.
                var messages = await Task.Run(() => _workflow.BuildRequestMessages(
                    currentProvider,
                    modelListSection,
                    MainWindow.WindowConversation(history, contextSettings),
                    mcpTools,
                    toolsEnabled,
                    modePromptSection));

                Providers.AiResponse response;
                ChatMessage? streamedMessage = null;
                try
                {
                    (response, streamedMessage) = await RequestProviderResponseAsync(messages, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    _owner.IsThinking = false;
                    _owner.AddSystemMessage("AI operation stopped.");
                    return;
                }
                catch
                {
                    _owner.IsThinking = false;
                    HandleErrorOutcome(_workflow.BuildErrorOutcome(new InvalidOperationException("AI operation failed.")));
                    return;
                }

                var outcome = _workflow.ParseResponse(response);
                if (outcome.Kind == ChatTurnWorkflowService.OutcomeKind.Error)
                {
                    _owner.IsThinking = false;
                    HandleErrorOutcome(outcome);
                    return;
                }

                if (outcome.Kind == ChatTurnWorkflowService.OutcomeKind.SuccessText)
                {
                    _owner.IsThinking = false;
                    var success = await _owner._chatTurnApplicationService.HandleSuccessTextAsync(
                        outcome.TextContent,
                        _owner._currentConversationId,
                        _owner.IsVisible);
                    var finalText = success.FinalText;

                    _owner._conversationHistory.Add(success.AssistantHistoryMessage);

                    if (streamedMessage != null)
                    {
                        var images = MainWindow.LoadChatImageSources(success.ImageReferences);
                        streamedMessage.Text = images == null
                            ? MainWindow.AppendImageFallbackLinks(finalText, success.ImageReferences)
                            : finalText;
                        streamedMessage.InlineImages = images;
                    }
                    else
                    {
                        var images = MainWindow.LoadChatImageSources(success.ImageReferences);
                        var now = DateTime.Now;
                        _owner.AddToUI(new ChatMessage
                        {
                            Sender = "AI",
                            Text = images == null
                                ? MainWindow.AppendImageFallbackLinks(finalText, success.ImageReferences)
                                : finalText,
                            Timestamp = now.ToString("HH:mm"),
                            MessageDate = now,
                            BackgroundBrush = MainWindow.AiBgBrush,
                            SenderForeground = MainWindow.AiFgBrush,
                            InlineImages = images
                        });
                    }

                    if (!_owner.IsVisible && _owner.TrayService != null)
                    {
                        var providerName = (_owner.ProviderComboBox.SelectedItem as Data.Provider)?.Name ?? "AI";
                        var preview = success.TrayPreview ?? finalText;
                        _owner.TrayService.ShowNotification(providerName, preview);
                        _owner.TrayService.ShowMainWindow();
                    }

                    _owner.SpeakResponseIfNeeded(finalText, wasVoice);
                    return;
                }

                _owner.IsThinking = false;
                var parsed = outcome.ParsedResponse!;

                try
                {
                    if (!string.IsNullOrEmpty(outcome.TextContent))
                    {
                        if (streamedMessage != null)
                        {
                            streamedMessage.Text = outcome.TextContent;
                        }
                        else
                        {
                            var now = DateTime.Now;
                            _owner.AddToUI(new ChatMessage
                            {
                                Sender = "AI",
                                Text = outcome.TextContent,
                                Timestamp = now.ToString("HH:mm"),
                                MessageDate = now,
                                BackgroundBrush = MainWindow.AiBgBrush,
                                SenderForeground = MainWindow.AiFgBrush
                            });
                        }
                    }

                    bool includeAssistantTextWithNextToolCall = !string.IsNullOrEmpty(parsed.TextContent);
                    foreach (var toolCall in parsed.ToolCalls)
                    {
                        string assistantTextForCurrentTool = includeAssistantTextWithNextToolCall ? parsed.TextContent : string.Empty;
                        includeAssistantTextWithNextToolCall = false;

                        if (toolCall.Tool == "switch_model")
                        {
                            await _owner.HandleSwitchModelAsync(assistantTextForCurrentTool, toolCall);
                            continue;
                        }

                        if (toolCall.Tool == "update_todo_list")
                        {
                            var todoResult = HandleUpdateTodoList(toolCall);
                            _owner._conversationHistory.Add(new ProviderChatMessage { Role = "tool", Content = todoResult });
                            continue;
                        }

                        if (toolCall.Tool == "ask_followup_question")
                        {
                            var answer = await HandleAskFollowUpQuestion(toolCall);
                            if (answer == null)
                                return;

                            _owner._conversationHistory.Add(new ProviderChatMessage { Role = "user", Content = answer });
                            await RunAiTurnAsync(iteration + 1, wasVoice, cancellationToken);
                            return;
                        }

                        if (toolCall.Tool == "attempt_completion")
                        {
                            var completion = _owner._chatTurnApplicationService.HandleAttemptCompletion(toolCall);
                            if (completion != null)
                            {
                                _owner._conversationHistory.Add(completion.AssistantHistoryMessage);
                                var now = DateTime.Now;
                                _owner.AddToUI(new ChatMessage
                                {
                                    Sender = "AI",
                                    Text = completion.FinalText,
                                    Timestamp = now.ToString("HH:mm"),
                                    MessageDate = now,
                                    BackgroundBrush = MainWindow.AiBgBrush,
                                    SenderForeground = MainWindow.AiFgBrush,
                                });
                            }

                            return;
                        }

                        var toolName = toolCall.Tool;
                        bool autoApprove = await _owner.ToolApprovals.DetermineAutoApproveAsync(toolName);
                        var (approved, approvalMsg) = await _owner.ToolApprovals.RequestApprovalAsync(toolCall, autoApprove);
                        var toolTurn = await _owner._chatTurnApplicationService.HandleToolExecutionAsync(
                            assistantTextForCurrentTool,
                            toolCall,
                            approved,
                            _owner._currentConversationId,
                            _owner._currentProvider?.Has(Providers.ProviderCapabilities.ImageInput) == true);

                        _owner._conversationHistory.Add(toolTurn.AssistantHistoryMessage);
                        approvalMsg.ToolCallStatus = toolTurn.ToolCallStatus;
                        if (approved)
                        {
                            _owner.ToolApprovals.ApplySessionState(toolCall);
                            if (toolTurn.ExecutionOutcome.ExecutionResult != null)
                                await _owner.ToolApprovals.PersistScreenshotAsync(toolTurn.ExecutionOutcome.ExecutionResult);
                        }

                        _owner._conversationHistory.Add(toolTurn.ToolHistoryMessage);

                        _owner.ScrollToBottom();
                    }
                }
                catch
                {
                    _owner.AddErrorMessage("Tool execution error.");
                    return;
                }

                await RunAiTurnAsync(iteration + 1, wasVoice, cancellationToken);
            }
        }
    }
}
