using System.Threading.Tasks;
using Aire.Services;
using Aire.Services.Workflows;

namespace Aire.AppLayer.Tools
{
    /// <summary>
    /// Completes a tool approval request after the user or policy has produced an approval decision.
    /// This keeps approval outcome persistence and execution branching out of UI entrypoints.
    /// </summary>
    public sealed class ToolApprovalExecutionApplicationService
    {
        /// <summary>
        /// Result of completing an approval request.
        /// </summary>
        /// <param name="Status">High-level completion state exposed to callers, such as completed or denied.</param>
        /// <param name="ToolCallStatus">Normalized status text shown in the UI for the approval message.</param>
        /// <param name="TextResult">User-facing text result for the completed approval.</param>
        /// <param name="ExecutionOutcome">Underlying tool execution outcome, including persisted side effects.</param>
        public sealed record ApprovalExecutionResult(
            string Status,
            string ToolCallStatus,
            string TextResult,
            ToolExecutionWorkflowService.ExecutionOutcome ExecutionOutcome);

        private readonly ToolApprovalPromptApplicationService _promptService;
        private readonly ToolExecutionWorkflowService _toolExecutionWorkflow;

        /// <summary>
        /// Creates the approval-completion service over the prompt and tool-execution seams.
        /// </summary>
        /// <param name="promptService">Service that normalizes approved and denied status text.</param>
        /// <param name="toolExecutionWorkflow">Workflow that persists and executes approved or denied tool calls.</param>
        public ToolApprovalExecutionApplicationService(
            ToolApprovalPromptApplicationService promptService,
            ToolExecutionWorkflowService toolExecutionWorkflow)
        {
            _promptService = promptService;
            _toolExecutionWorkflow = toolExecutionWorkflow;
        }

        /// <summary>
        /// Applies the approval decision, executes or denies the tool, and returns the normalized completion state.
        /// </summary>
        /// <param name="request">Tool call that finished approval.</param>
        /// <param name="approved">Whether the tool was approved for execution.</param>
        /// <param name="conversationId">Current conversation id, if transcript persistence should occur.</param>
        /// <returns>The normalized approval completion result, including the persisted execution outcome.</returns>
        public async Task<ApprovalExecutionResult> CompleteAsync(
            ToolCallRequest request,
            bool approved,
            int? conversationId)
        {
            var completionPlan = _promptService.BuildCompletionPlan(approved, request.Description);
            var executionOutcome = await _toolExecutionWorkflow.ExecuteAsync(request, approved, conversationId);

            return new ApprovalExecutionResult(
                approved ? "completed" : "denied",
                completionPlan.ToolCallStatus,
                approved ? executionOutcome.ToolResult : "Tool execution was denied.",
                executionOutcome);
        }
    }
}
