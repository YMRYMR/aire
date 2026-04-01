using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Aire.Services.Workflows
{
    /// <summary>
    /// Parses and formats the non-UI parts of tool-follow-up workflows so MainWindow only renders the result.
    /// </summary>
    public sealed class ToolFollowUpWorkflowService
    {
        /// <summary>
        /// Provider-agnostic todo item extracted from an <c>update_todo_list</c> tool call.
        /// </summary>
        public sealed record TodoTask(string Id, string Description, string Status);

        /// <summary>
        /// Follow-up question extracted from an <c>ask_followup_question</c> tool call.
        /// </summary>
        public sealed record FollowUpQuestionRequest(string Question, IReadOnlyList<string> Options);

        /// <summary>
        /// Builds the assistant-history entry that preserves both plain text and the emitted tool call.
        /// </summary>
        /// <param name="textContent">Visible assistant text emitted before the tool call.</param>
        /// <param name="rawJson">Raw tool-call JSON body.</param>
        /// <returns>Conversation-history text stored for the assistant turn.</returns>
        public string BuildAssistantToolCallContent(string textContent, string rawJson)
            => string.IsNullOrEmpty(textContent)
                ? $"<tool_call>{rawJson}</tool_call>"
                : $"{textContent}\n<tool_call>{rawJson}</tool_call>";

        /// <summary>
        /// Builds the synthetic history message fed back to the provider after a tool executes.
        /// </summary>
        /// <param name="toolName">Tool that was executed or denied.</param>
        /// <param name="toolResult">Result text returned by the tool path.</param>
        /// <returns>The normalized provider-facing history entry.</returns>
        public string BuildToolResultHistoryContent(string toolName, string toolResult)
            => $"[File system result — {toolName}]:\n{toolResult}";

        /// <summary>
        /// Extracts a file-system-like path from a tool request for auditing.
        /// </summary>
        /// <param name="request">Tool call being executed.</param>
        /// <returns>The most relevant path-like argument, or an empty string when none is present.</returns>
        public string GetPathFromRequest(ToolCallRequest request)
        {
            foreach (var key in new[] { "path", "from", "directory" })
            {
                if (request.Parameters.TryGetProperty(key, out var value))
                    return value.GetString() ?? string.Empty;
            }

            return string.Empty;
        }

        /// <summary>
        /// Parses provider tool parameters into a normalized todo-task list.
        /// </summary>
        /// <param name="parameters">Tool-call parameter payload.</param>
        /// <returns>Todo tasks extracted from the payload.</returns>
        public IReadOnlyList<TodoTask> ParseTodoTasks(JsonElement parameters)
        {
            var items = new List<TodoTask>();
            if (!parameters.TryGetProperty("tasks", out var tasksElement))
                return items;

            if (tasksElement.ValueKind == JsonValueKind.Array)
            {
                AddTasksFromArray(tasksElement, items);
                return items;
            }

            if (tasksElement.ValueKind != JsonValueKind.String)
                return items;

            var text = tasksElement.GetString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
                return items;

            if (text.TrimStart().StartsWith('['))
            {
                try
                {
                    using var doc = JsonDocument.Parse(text);
                    AddTasksFromArray(doc.RootElement, items);
                    return items;
                }
                catch (JsonException)
                {
                    // Not valid JSON — fall through to plain-text treatment below
                }
            }

            items.Add(new TodoTask(string.Empty, text, "pending"));
            return items;
        }

        /// <summary>
        /// Builds the short status text shown after a todo list update.
        /// </summary>
        /// <param name="tasks">Normalized todo tasks.</param>
        /// <returns>A short human-readable summary of the todo state.</returns>
        public string BuildTodoUpdateStatus(IReadOnlyCollection<TodoTask> tasks)
        {
            int done = tasks.Count(task => string.Equals(task.Status, "completed", StringComparison.OrdinalIgnoreCase));
            return $"Todo list updated: {tasks.Count} task(s), {done} completed.";
        }

        /// <summary>
        /// Parses a normalized follow-up question request from provider tool parameters.
        /// </summary>
        /// <param name="parameters">Tool-call parameter payload.</param>
        /// <returns>The question payload, or <see langword="null"/> when no valid question was present.</returns>
        public FollowUpQuestionRequest? ParseFollowUpQuestion(JsonElement parameters)
        {
            string question = parameters.TryGetProperty("question", out var qEl)
                ? qEl.GetString() ?? string.Empty
                : string.Empty;

            if (string.IsNullOrWhiteSpace(question))
                return null;

            var options = new List<string>();
            if (parameters.TryGetProperty("options", out var optionsElement))
                AddOptions(optionsElement, options);

            return new FollowUpQuestionRequest(question, options);
        }

        private static void AddTasksFromArray(JsonElement tasksArray, ICollection<TodoTask> items)
        {
            if (tasksArray.ValueKind != JsonValueKind.Array)
                return;

            foreach (var task in tasksArray.EnumerateArray())
            {
                var id = task.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? string.Empty : string.Empty;
                var description = task.TryGetProperty("description", out var descEl) ? descEl.GetString() ?? string.Empty : string.Empty;
                var status = task.TryGetProperty("status", out var statusEl) ? statusEl.GetString() ?? "pending" : "pending";
                if (!string.IsNullOrWhiteSpace(description))
                    items.Add(new TodoTask(id, description, status));
            }
        }

        private static void AddOptions(JsonElement optionsElement, ICollection<string> options)
        {
            if (optionsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var option in optionsElement.EnumerateArray())
                {
                    var text = option.GetString();
                    if (!string.IsNullOrWhiteSpace(text))
                        options.Add(text);
                }
                return;
            }

            if (optionsElement.ValueKind != JsonValueKind.String)
                return;

            var raw = optionsElement.GetString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(raw))
                return;

            if (raw.TrimStart().StartsWith('['))
            {
                try
                {
                    using var doc = JsonDocument.Parse(raw);
                    AddOptions(doc.RootElement, options);
                    return;
                }
                catch (JsonException)
                {
                    // Not valid JSON — fall through to comma/newline splitting below
                }
            }

            foreach (var option in raw.Split(new[] { ',', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                options.Add(option);

            if (options.Count == 0)
                options.Add(raw);
        }
    }
}
