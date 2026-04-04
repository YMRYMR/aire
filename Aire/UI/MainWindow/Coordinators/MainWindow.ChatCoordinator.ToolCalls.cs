using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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
            private void HandleErrorOutcome(ChatTurnWorkflowService.ChatTurnOutcome outcome)
            {
                if (outcome.CooldownReason != CooldownReason.None && _owner._currentProviderId.HasValue)
                {
                    _owner._availabilityTracker.SetCooldown(_owner._currentProviderId.Value, outcome.CooldownReason, outcome.CooldownMessage ?? string.Empty);
                    _owner.AddErrorMessage(outcome.ErrorMessage ?? string.Empty, outcome.CooldownMessage);
                }
                else
                {
                    _owner.AddErrorMessage(outcome.ErrorMessage ?? string.Empty);
                }
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
