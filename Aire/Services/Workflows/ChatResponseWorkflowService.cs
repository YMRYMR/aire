using System;

namespace Aire.Services.Workflows
{
    /// <summary>
    /// Normalizes provider response text into the small pieces of UI-friendly state used by the chat coordinator.
    /// </summary>
    public sealed class ChatResponseWorkflowService
    {
        /// <summary>
        /// Ensures that empty provider text is surfaced as a readable placeholder instead of a blank message.
        /// </summary>
        /// <param name="textContent">Raw provider text content.</param>
        /// <returns>User-visible assistant text.</returns>
        public string NormalizeFinalText(string textContent)
            => string.IsNullOrEmpty(textContent) ? "(empty response)" : textContent;

        /// <summary>
        /// Builds the short tray-notification preview shown when the app is hidden.
        /// </summary>
        /// <param name="textContent">Full assistant response text.</param>
        /// <param name="maxLength">Maximum preview length before truncation.</param>
        /// <returns>A compact one-line preview.</returns>
        public string BuildTrayPreview(string textContent, int maxLength = 80)
            => textContent.Length > maxLength
                ? textContent[..maxLength].TrimEnd() + "\u2026"
                : textContent;

        /// <summary>
        /// Extracts the completion result from an <c>attempt_completion</c> tool call.
        /// </summary>
        /// <param name="request">Tool call emitted by the model.</param>
        /// <returns>The completion text, or an empty string when none was provided.</returns>
        public string ExtractCompletionResult(ToolCallRequest request)
            => request.Parameters.TryGetProperty("result", out var resultElement)
                ? resultElement.GetString() ?? string.Empty
                : string.Empty;
    }
}
