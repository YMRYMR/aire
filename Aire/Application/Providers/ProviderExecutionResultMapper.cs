using Aire.AppLayer.Abstractions;
using Aire.Domain.Providers;
using Aire.Providers;
using Aire.Services;

namespace Aire.AppLayer.Providers
{
    /// <summary>
    /// Converts the current legacy provider response and parser output into the new
    /// shared provider-execution contracts. This lets the application layer start
    /// using adapter-style semantics before every provider is migrated.
    /// </summary>
    public static class ProviderExecutionResultMapper
    {
        /// <summary>
        /// Maps a legacy provider response into a shared provider execution result.
        /// </summary>
        /// <param name="response">Normalized legacy provider response.</param>
        /// <returns>Shared execution result using workflow and tool intents.</returns>
        public static ProviderExecutionResult FromLegacyResponse(AiResponse response)
        {
            if (!response.IsSuccess)
                return ProviderExecutionResult.Failed(response.ErrorMessage ?? string.Empty, response.Duration);

            ParsedAiResponse parsed = ToolCallParser.Parse(response.Content ?? string.Empty);
            WorkflowIntent intent = BuildWorkflowIntent(parsed);
            return ProviderExecutionResult.Succeeded(intent, response.Content ?? string.Empty, response.TokensUsed, response.Duration);
        }

        /// <summary>
        /// Maps a parsed legacy response into a workflow intent.
        /// </summary>
        /// <param name="parsed">Parsed text plus optional tool call from the legacy parser.</param>
        /// <returns>Provider-independent workflow intent.</returns>
        public static WorkflowIntent BuildWorkflowIntent(ParsedAiResponse parsed)
        {
            if (!parsed.HasToolCall || parsed.ToolCall == null)
            {
                string finalText = string.IsNullOrEmpty(parsed.TextContent) ? "(empty response)" : parsed.TextContent;
                return WorkflowIntent.AssistantText(finalText);
            }

            string displayText = parsed.TextContent;
            string parametersJson = parsed.ToolCall.Parameters.ValueKind == System.Text.Json.JsonValueKind.Undefined
                ? "{}"
                : parsed.ToolCall.Parameters.GetRawText();

            return parsed.ToolCall.Tool switch
            {
                "switch_model" => WorkflowIntent.SwitchModel(displayText, parametersJson, parsed.ToolCall.RawJson),
                "update_todo_list" => WorkflowIntent.UpdateTodoList(displayText, parametersJson, parsed.ToolCall.RawJson),
                "ask_followup_question" => WorkflowIntent.FollowUpQuestion(displayText, parametersJson, parsed.ToolCall.RawJson),
                "attempt_completion" => WorkflowIntent.AttemptCompletion(displayText, parametersJson, parsed.ToolCall.RawJson),
                _ => WorkflowIntent.ToolCall(displayText, new ToolIntent
                {
                    ToolName = parsed.ToolCall.Tool,
                    Parameters = parsed.ToolCall.Parameters.Clone(),
                    Description = parsed.ToolCall.Description,
                    RawPayload = parsed.ToolCall.RawJson
                })
            };
        }
    }
}
