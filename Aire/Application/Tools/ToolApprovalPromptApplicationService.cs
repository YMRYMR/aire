namespace Aire.AppLayer.Tools
{
    /// <summary>
    /// Application-layer decisions for creating and completing tool-approval prompts across UI entrypoints.
    /// </summary>
    public sealed class ToolApprovalPromptApplicationService
    {
        /// <summary>
        /// Initial UI behavior for a pending tool approval request.
        /// </summary>
        public sealed record ApprovalPromptPlan(
            bool IsApprovalPending,
            bool AutoApproveImmediately,
            bool ShouldRevealWindow);

        /// <summary>
        /// Completion state for a finished approval request.
        /// </summary>
        public sealed record ApprovalCompletionPlan(
            string ToolCallStatus,
            bool WasDenied);

        /// <summary>
        /// Determines whether an approval prompt should wait for user input and whether the main window should be shown.
        /// </summary>
        /// <param name="autoApprove">Whether the tool can run without user interaction.</param>
        /// <param name="isWindowVisible">Whether the main window is currently visible to the user.</param>
        /// <returns>Prompt state describing whether approval is pending and whether the window should be revealed.</returns>
        public ApprovalPromptPlan BuildPromptPlan(bool autoApprove, bool isWindowVisible)
            => new(
                IsApprovalPending: !autoApprove,
                AutoApproveImmediately: autoApprove,
                ShouldRevealWindow: !autoApprove && !isWindowVisible);

        /// <summary>
        /// Builds the final approval status text shown after the user approves or denies a tool.
        /// </summary>
        /// <param name="approved">Whether the user approved the tool execution.</param>
        /// <param name="toolDescription">Human-readable tool description used for approved status text.</param>
        /// <returns>The normalized completion status for the approval message.</returns>
        public ApprovalCompletionPlan BuildCompletionPlan(bool approved, string toolDescription)
            => approved
                ? new ApprovalCompletionPlan($"\u2713 {toolDescription}", WasDenied: false)
                : new ApprovalCompletionPlan("\u2717 Denied", WasDenied: true);
    }
}
