using System.IO;
using System.Threading.Tasks;
using Aire.Providers;
using Aire.Services;
using Aire.Services.Workflows;
using ProviderChatMessage = Aire.Providers.ChatMessage;

namespace Aire.AppLayer.Chat
{
    /// <summary>
    /// Application-layer orchestration for the non-UI side effects of one chat turn.
    /// </summary>
    public sealed class ChatTurnApplicationService
    {
        /// <summary>
        /// Result of a successful assistant text response.
        /// </summary>
        public sealed record SuccessTextResult(
            string FinalText,
            string? TrayPreview,
            ProviderChatMessage AssistantHistoryMessage,
            string? ImageReference);

        /// <summary>
        /// Result of an <c>attempt_completion</c> tool call.
        /// </summary>
        public sealed record CompletionResult(
            string FinalText,
            ProviderChatMessage AssistantHistoryMessage);

        /// <summary>
        /// Result of a tool-execution turn after approval or denial.
        /// </summary>
        public sealed record ToolExecutionTurnResult(
            string AssistantToolCallContent,
            string ToolResult,
            string ToolCallStatus,
            ProviderChatMessage AssistantHistoryMessage,
            ProviderChatMessage ToolHistoryMessage,
            ToolExecutionWorkflowService.ExecutionOutcome ExecutionOutcome);

        private readonly ChatSessionApplicationService _chatSessionService;
        private readonly ChatResponseWorkflowService _responseWorkflow = new();
        private readonly AssistantImageResponseApplicationService _imageResponseWorkflow = new();
        private readonly ToolFollowUpWorkflowService _toolFollowUpWorkflow = new();
        private readonly ToolExecutionWorkflowService _toolExecutionWorkflow;

        /// <summary>
        /// Creates the chat-turn application service over the chat-session and tool-execution seams.
        /// </summary>
        public ChatTurnApplicationService(
            ChatSessionApplicationService chatSessionService,
            ToolExecutionWorkflowService toolExecutionWorkflow)
        {
            _chatSessionService = chatSessionService;
            _toolExecutionWorkflow = toolExecutionWorkflow;
        }

        /// <summary>
        /// Persists and normalizes a plain successful assistant text response.
        /// </summary>
        public async Task<SuccessTextResult> HandleSuccessTextAsync(
            string textContent,
            int? conversationId,
            bool isWindowVisible,
            int trayPreviewLength = 80)
        {
            var parsed = _imageResponseWorkflow.Parse(textContent);
            var finalText = string.IsNullOrWhiteSpace(parsed.Text) && parsed.ImageReference != null
                ? string.Empty
                : _responseWorkflow.NormalizeFinalText(parsed.Text);

            if (conversationId.HasValue)
                await _chatSessionService.PersistAssistantMessageAsync(conversationId.Value, finalText, parsed.ImageReference);

            return new SuccessTextResult(
                finalText,
                isWindowVisible ? null : _responseWorkflow.BuildTrayPreview(string.IsNullOrWhiteSpace(finalText) ? parsed.Text : finalText, trayPreviewLength),
                new ProviderChatMessage { Role = "assistant", Content = string.IsNullOrWhiteSpace(finalText) ? parsed.Text : finalText },
                parsed.ImageReference);
        }

        /// <summary>
        /// Extracts the completion text from an <c>attempt_completion</c> tool call.
        /// </summary>
        public CompletionResult? HandleAttemptCompletion(ToolCallRequest request)
        {
            var result = _responseWorkflow.ExtractCompletionResult(request);
            if (string.IsNullOrWhiteSpace(result))
                return null;

            return new CompletionResult(
                result,
                new ProviderChatMessage { Role = "assistant", Content = result });
        }

        /// <summary>
        /// Executes or denies one tool turn and prepares the provider-history entries that follow.
        /// </summary>
        public async Task<ToolExecutionTurnResult> HandleToolExecutionAsync(
            ParsedAiResponse parsed,
            bool approved,
            int? conversationId,
            bool includeScreenshotImageInHistory)
        {
            var toolCall = parsed.ToolCall!;
            var executionOutcome = await _toolExecutionWorkflow.ExecuteAsync(toolCall, approved, conversationId);
            var assistantToolCallContent = _toolFollowUpWorkflow.BuildAssistantToolCallContent(parsed.TextContent, toolCall.RawJson);

            var toolHistoryMessage = new ProviderChatMessage
            {
                Role = "user",
                Content = executionOutcome.HistoryContent
            };

            if (includeScreenshotImageInHistory &&
                toolCall.Tool == "take_screenshot" &&
                executionOutcome.ExecutionResult?.ScreenshotPath != null)
            {
                try { toolHistoryMessage.ImageBytes = await File.ReadAllBytesAsync(executionOutcome.ExecutionResult.ScreenshotPath); }
                catch (Exception ex) { AppLogger.Error("ChatTurn.BuildHistory", "Failed to read screenshot for history", ex); }
                toolHistoryMessage.ImageMimeType = "image/png";
            }

            return new ToolExecutionTurnResult(
                assistantToolCallContent,
                executionOutcome.ToolResult,
                executionOutcome.ToolCallStatus,
                new ProviderChatMessage { Role = "assistant", Content = assistantToolCallContent },
                toolHistoryMessage,
                executionOutcome);
        }
    }
}
