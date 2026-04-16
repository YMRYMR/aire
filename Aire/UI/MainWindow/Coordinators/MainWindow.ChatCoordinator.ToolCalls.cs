using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Aire.Services;
using Aire.Services.Workflows;
using ChatMessage = Aire.UI.MainWindow.Models.ChatMessage;
using ProviderChatMessage = Aire.Providers.ChatMessage;
using TodoItem = Aire.UI.MainWindow.Models.TodoItem;

namespace Aire
{
    public partial class MainWindow
    {
        private sealed partial class ChatCoordinator
        {
            /// <summary>
            /// Applies a todo-list tool update to the current UI message and returns a short status summary.
            /// </summary>
            private async Task HandleErrorOutcome(ChatTurnWorkflowService.ChatTurnOutcome outcome)
            {
                if (outcome.CooldownReason != CooldownReason.None && _owner._currentProviderId.HasValue)
                {
                    _owner._availabilityTracker.SetCooldown(_owner._currentProviderId.Value, outcome.CooldownReason, outcome.CooldownMessage ?? string.Empty);
                    await _owner.AddErrorMessageAsync(outcome.ErrorMessage ?? string.Empty, outcome.CooldownMessage);
                }
                else
                {
                    await _owner.AddErrorMessageAsync(outcome.ErrorMessage ?? string.Empty);
                }
            }

            private static bool IsOrchestratorToolCallCutOff(ChatTurnWorkflowService.ChatTurnOutcome outcome)
            {
                var text = string.Join(' ', new[] { outcome.ErrorMessage, outcome.TextContent }.Where(value => !string.IsNullOrWhiteSpace(value)));
                if (string.IsNullOrWhiteSpace(text))
                    return false;

                return text.Contains("cut off before the tool call could complete", StringComparison.OrdinalIgnoreCase)
                    || text.Contains("max_tokens limit reached", StringComparison.OrdinalIgnoreCase)
                    || text.Contains("response was cut off", StringComparison.OrdinalIgnoreCase)
                    || text.Contains("tool call could complete", StringComparison.OrdinalIgnoreCase);
            }

            private static bool IsOrchestratorCutOffOutcome(ChatTurnWorkflowService.ChatTurnOutcome outcome)
            {
                if (IsOrchestratorToolCallCutOff(outcome))
                    return true;

                var text = string.Join(' ', new[] { outcome.ErrorMessage, outcome.TextContent }.Where(value => !string.IsNullOrWhiteSpace(value)));
                return text.Contains("response was cut off", StringComparison.OrdinalIgnoreCase)
                    || text.Contains("max_tokens limit reached", StringComparison.OrdinalIgnoreCase)
                    || text.Contains("try asking the model to break the task into smaller steps", StringComparison.OrdinalIgnoreCase);
            }

            private async Task<bool> TryRecoverOrchestratorModelCutoffAsync(
                ChatTurnWorkflowService.ChatTurnOutcome outcome,
                int iteration,
                bool wasVoice,
                CancellationToken cancellationToken,
                bool recoveryTurn)
            {
                if (_owner._agentModeService?.IsActive != true)
                    return false;

                if (!IsOrchestratorCutOffOutcome(outcome))
                    return false;

                if (!_owner._currentProviderId.HasValue)
                    return false;

                var currentProviderId = _owner._currentProviderId.Value;
                if (recoveryTurn)
                {
                    await _owner.AddErrorMessageAsync(
                        "The model cut off again while retrying. Orchestrator Mode has been paused so you can raise max_tokens, simplify the goal, or try a different provider.");
                    _owner.StopOrchestratorMode("model cut off repeatedly");
                    return true;
                }

                var narration = "The current model cut off before it could finish the step. I’m switching to another model, raising the retry budget, and trying again.";
                await _owner.AddOrchestratorNarrativeAsync(narration);

                _owner._agentModeService?.RecordTaskFailure(
                    "provider-response",
                    $"{currentProviderId}:{outcome.ErrorMessage ?? outcome.TextContent ?? "provider cut off"}");

                var switched = await _owner.TrySwitchOrchestratorFallbackProviderAsync(currentProviderId);

                if (!switched)
                {
                    await _owner.AddErrorMessageAsync(
                        "The current model cut off before it could finish the step, and no fallback model was available.\n\nTry increasing max_tokens in Settings or enabling another provider.");
                    return true;
                }

                if (_owner._agentModeService?.IsActive == true)
                {
                    await RunAiTurnAsync(iteration + 1, wasVoice, cancellationToken, recoveryTurn: true);
                    return true;
                }

                return true;
            }

            private string HandleUpdateTodoList(ParsedAiResponse parsed)
                => HandleUpdateTodoList(parsed.ToolCall!);

            private string HandleUpdateTodoList(ToolCallRequest toolCall)
            {
                var update = _owner._chatInteractionApplicationService.BuildTodoUpdate(toolCall);
                var items = new System.Collections.ObjectModel.ObservableCollection<TodoItem>(
                    update.Items.Select(task => new TodoItem
                    {
                        Id = task.Id,
                        Description = task.Description,
                        Status = task.Status
                    }));

                if (_owner._todoListMessage == null)
                {
                    var now = DateTime.Now;
                    _owner._todoListMessage = new ChatMessage
                    {
                        Sender = "AI",
                        Text = string.Empty,
                        Timestamp = now.ToString("HH:mm"),
                        MessageDate = now,
                        BackgroundBrush = MainWindow.AiBgBrush,
                        SenderForeground = MainWindow.AiFgBrush,
                        TodoItems = items,
                    };
                    _owner.AddToUI(_owner._todoListMessage);
                }
                else
                {
                    _owner._todoListMessage.TodoItems = items;
                }

                return update.StatusText;
            }

            private async Task<string?> HandleAskFollowUpQuestion(ParsedAiResponse parsed)
                => await HandleAskFollowUpQuestion(parsed.ToolCall!);

            private async Task<string?> HandleAskFollowUpQuestion(ToolCallRequest toolCall)
            {
                var prompt = _owner._chatInteractionApplicationService.BuildFollowUpPrompt(toolCall);
                if (prompt == null)
                    return null;

                TaskCompletionSource<string>? tcs = prompt.Options.Count > 0
                    ? new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously)
                    : null;

                var msg = new ChatMessage
                {
                    Sender = "AI",
                    Text = string.Empty,
                    Timestamp = DateTime.Now.ToString("HH:mm"),
                    MessageDate = DateTime.Now,
                    BackgroundBrush = MainWindow.AiBgBrush,
                    SenderForeground = MainWindow.AiFgBrush,
                    FollowUpQuestion = prompt.Question,
                    FollowUpOptions = prompt.Options.Count > 0 ? prompt.Options.ToList() : null,
                    AnswerTcs = tcs,
                };
                _owner.AddToUI(msg);

                _owner._conversationHistory.Add(new ProviderChatMessage { Role = "assistant", Content = prompt.AssistantHistoryMessage });

                if (tcs == null)
                    return null;
                return await tcs.Task;
            }
        }
    }
}
