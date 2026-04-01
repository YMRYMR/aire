using System.Collections.Generic;
using System.Linq;
using Aire.Services;
using Aire.Services.Workflows;

namespace Aire.AppLayer.Chat
{
    /// <summary>
    /// Application-layer shaping for follow-up prompts and todo-list tool calls before the UI renders them.
    /// </summary>
    public sealed class ChatInteractionApplicationService
    {
        /// <summary>
        /// View-neutral todo item extracted from an <c>update_todo_list</c> tool call.
        /// </summary>
        public sealed record TodoItemState(string Id, string Description, string Status);

        /// <summary>
        /// Result of parsing and summarizing an <c>update_todo_list</c> tool call.
        /// </summary>
        public sealed record TodoUpdateResult(
            IReadOnlyList<TodoItemState> Items,
            string StatusText);

        /// <summary>
        /// Result of parsing an <c>ask_followup_question</c> tool call for UI display and history persistence.
        /// </summary>
        public sealed record FollowUpPromptResult(
            string Question,
            IReadOnlyList<string> Options,
            string AssistantHistoryMessage);

        private readonly ToolFollowUpWorkflowService _toolFollowUpWorkflow = new();

        /// <summary>
        /// Parses a todo-list tool call into view-neutral items and a short status summary.
        /// </summary>
        /// <param name="parsed">Provider response containing the tool call.</param>
        /// <returns>The normalized todo items and summary text.</returns>
        public TodoUpdateResult BuildTodoUpdate(ParsedAiResponse parsed)
        {
            var tasks = _toolFollowUpWorkflow.ParseTodoTasks(parsed.ToolCall!.Parameters);
            var items = tasks
                .Select(task => new TodoItemState(task.Id, task.Description, task.Status))
                .ToList();

            return new TodoUpdateResult(
                items,
                _toolFollowUpWorkflow.BuildTodoUpdateStatus(tasks));
        }

        /// <summary>
        /// Parses a follow-up question tool call into a view-neutral prompt.
        /// </summary>
        /// <param name="parsed">Provider response containing the tool call.</param>
        /// <returns>The prompt information, or <see langword="null"/> when no valid question was present.</returns>
        public FollowUpPromptResult? BuildFollowUpPrompt(ParsedAiResponse parsed)
        {
            var request = _toolFollowUpWorkflow.ParseFollowUpQuestion(parsed.ToolCall!.Parameters);
            if (request == null)
                return null;

            return new FollowUpPromptResult(
                request.Question,
                request.Options,
                request.Question);
        }
    }
}
