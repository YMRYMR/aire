using System;
using System.Collections.Generic;
using Aire.Providers;
using Aire.Services.Mcp;
using ProviderChatMessage = Aire.Providers.ChatMessage;

namespace Aire.Services.Workflows
{
    /// <summary>
    /// Converts raw provider responses into high-level workflow outcomes that the UI coordinator can execute.
    /// This keeps prompt construction, response parsing, and branch selection out of window code.
    /// </summary>
    public sealed class ChatTurnWorkflowService
    {
        /// <summary>
        /// Logical branch selected from a single AI turn.
        /// </summary>
        public enum OutcomeKind
        {
            SuccessText,
            SwitchModel,
            UpdateTodoList,
            AskFollowUpQuestion,
            AttemptCompletion,
            ExecuteTool,
            Error,
            Canceled
        }

        /// <summary>
        /// Parsed result of one AI turn, including both normal text and tool/workflow branches.
        /// </summary>
        public sealed record ChatTurnOutcome(
            OutcomeKind Kind,
            string TextContent,
            ParsedAiResponse? ParsedResponse = null,
            string? ErrorMessage = null,
            CooldownReason CooldownReason = CooldownReason.None,
            string? CooldownMessage = null);

        /// <summary>
        /// Builds the provider-facing message list, including the synthetic system prompt when needed.
        /// </summary>
        /// <param name="provider">Currently active provider, used to decide whether tool/system prompts are needed.</param>
        /// <param name="modelListSection">Rendered model-switch section appended to the synthetic system prompt.</param>
        /// <param name="conversationHistory">Conversation history converted into provider message format.</param>
        /// <param name="mcpTools">Optional MCP tool definitions to expose in the system prompt.</param>
        /// <param name="toolsEnabled">When false, the tool system prompt is omitted entirely (plain chat mode).</param>
        /// <returns>The ordered provider message list for the next AI call.</returns>
        public IReadOnlyList<ProviderChatMessage> BuildRequestMessages(
            IAiProvider? provider,
            string modelListSection,
            IReadOnlyList<ProviderChatMessage> conversationHistory,
            IReadOnlyList<McpToolDefinition>? mcpTools = null,
            bool toolsEnabled = true)
        {
            var messages = new List<ProviderChatMessage>();
            if (toolsEnabled &&
                (provider?.Has(ProviderCapabilities.ToolCalling) == true ||
                 provider?.Has(ProviderCapabilities.SystemPrompt) == true))
            {
                var sysPrompt = provider.ToolOutputFormat switch
                {
                    ToolOutputFormat.Hermes          => FileSystemSystemPrompt.HermesToolCallingText,
                    ToolOutputFormat.React           => FileSystemSystemPrompt.ReactToolCallingText,
                    ToolOutputFormat.NativeToolCalls => FileSystemSystemPrompt.NativeToolCallingText,
                    _                                => FileSystemSystemPrompt.Text,   // AireText
                };

                sysPrompt += modelListSection;

                if (mcpTools != null && mcpTools.Count > 0)
                    sysPrompt += McpToolPromptBuilder.BuildSection(mcpTools);

                messages.Add(new ProviderChatMessage { Role = "system", Content = sysPrompt });
            }

            messages.AddRange(conversationHistory);
            return messages;
        }

        /// <summary>
        /// Maps transport/provider exceptions into the same cooldown-aware error shape used by the UI.
        /// </summary>
        /// <param name="ex">The thrown exception from the provider call path.</param>
        /// <returns>A normalized error outcome that preserves cooldown classification.</returns>
        public ChatTurnOutcome BuildErrorOutcome(Exception ex)
        {
            var cooldownReason = ProviderErrorClassifier.Classify(ex, out var cooldownMessage);
            return new ChatTurnOutcome(
                OutcomeKind.Error,
                string.Empty,
                ErrorMessage: ex.Message,
                CooldownReason: cooldownReason,
                CooldownMessage: cooldownMessage);
        }

        /// <summary>
        /// Maps an unsuccessful provider response into a workflow error outcome.
        /// </summary>
        /// <param name="errorText">The provider-reported error text.</param>
        /// <returns>A normalized error outcome that preserves cooldown classification.</returns>
        public ChatTurnOutcome BuildFailureOutcome(string errorText)
        {
            var syntheticEx = new Exception(errorText);
            var cooldownReason = ProviderErrorClassifier.Classify(syntheticEx, out var cooldownMessage);
            return new ChatTurnOutcome(
                OutcomeKind.Error,
                string.Empty,
                ErrorMessage: errorText,
                CooldownReason: cooldownReason,
                CooldownMessage: cooldownMessage);
        }

        /// <summary>
        /// Parses the provider response and selects the next workflow branch.
        /// </summary>
        /// <param name="response">Raw provider response returned by the chat service.</param>
        /// <returns>The branch that the UI coordinator should execute next.</returns>
        public ChatTurnOutcome ParseResponse(AiResponse response)
        {
            if (!response.IsSuccess)
                return BuildFailureOutcome(response.ErrorMessage ?? string.Empty);

            var parsed = ToolCallParser.Parse(response.Content ?? string.Empty);
            if (!parsed.HasToolCall)
            {
                var finalText = string.IsNullOrEmpty(parsed.TextContent) ? "(empty response)" : parsed.TextContent;
                return new ChatTurnOutcome(OutcomeKind.SuccessText, finalText, parsed);
            }

            return parsed.ToolCall!.Tool switch
            {
                "switch_model" => new ChatTurnOutcome(OutcomeKind.SwitchModel, parsed.TextContent, parsed),
                "update_todo_list" => new ChatTurnOutcome(OutcomeKind.UpdateTodoList, parsed.TextContent, parsed),
                "ask_followup_question" => new ChatTurnOutcome(OutcomeKind.AskFollowUpQuestion, parsed.TextContent, parsed),
                "attempt_completion" => new ChatTurnOutcome(OutcomeKind.AttemptCompletion, parsed.TextContent, parsed),
                _ => new ChatTurnOutcome(OutcomeKind.ExecuteTool, parsed.TextContent, parsed),
            };
        }
    }
}
