using System.Threading.Tasks;
using Aire.AppLayer.Abstractions;
using Aire.Services;

namespace Aire.Services.Workflows
{
    /// <summary>
    /// Executes approved or denied tool requests and persists the non-UI follow-up effects.
    /// </summary>
    public sealed class ToolExecutionWorkflowService
    {
        public sealed record ExecutionOutcome(
            bool Approved,
            string ToolResult,
            string ToolCallStatus,
            string ToolPath,
            ToolExecutionResult? ExecutionResult,
            string HistoryContent);

        private readonly ToolExecutionService _toolExecutionService;
        private readonly IConversationRepository _conversations;
        private readonly ISettingsRepository _settings;
        private readonly ToolFollowUpWorkflowService _followUpWorkflow = new();

        /// <summary>
        /// Creates the workflow service for tool execution side effects.
        /// </summary>
        /// <param name="toolExecutionService">Dispatcher that executes the underlying tool.</param>
        /// <param name="conversations">Conversation repository used for transcript persistence.</param>
        /// <param name="settings">Settings/audit repository used for file-access logging.</param>
        public ToolExecutionWorkflowService(
            ToolExecutionService toolExecutionService,
            IConversationRepository conversations,
            ISettingsRepository settings)
        {
            _toolExecutionService = toolExecutionService;
            _conversations = conversations;
            _settings = settings;
        }

        /// <summary>
        /// Executes or denies one tool request, then persists the audit and transcript effects.
        /// </summary>
        /// <param name="request">Tool call being handled.</param>
        /// <param name="approved">Whether the user or policy allowed the tool to run.</param>
        /// <param name="conversationId">Current conversation id, if transcript persistence should occur.</param>
        /// <returns>The normalized execution outcome for the UI coordinator.</returns>
        public async Task<ExecutionOutcome> ExecuteAsync(ToolCallRequest request, bool approved, int? conversationId)
        {
            var toolPath = _followUpWorkflow.GetPathFromRequest(request);

            if (approved)
            {
                var executionResult = await _toolExecutionService.ExecuteAsync(request);
                var status = string.Format(LocalizationService.S("toolStatus.approved", "\u2713 {0}"), request.Description);

                if (conversationId.HasValue)
                    await _conversations.SaveMessageAsync(conversationId.Value, "tool", status);

                await _settings.LogFileAccessAsync(request.Tool, toolPath, true);

                return new ExecutionOutcome(
                    true,
                    executionResult.TextResult,
                    status,
                    toolPath,
                    executionResult,
                    _followUpWorkflow.BuildToolResultHistoryContent(request.Tool, executionResult.TextResult));
            }

            const string deniedResult = "[Operation denied by user]";
            var deniedStatus = LocalizationService.S("toolStatus.denied", "\u2717 Denied");
            var deniedAction = string.IsNullOrWhiteSpace(request.Description)
                ? request.Tool.Replace('_', ' ')
                : request.Description;
            var deniedTranscript = $"{deniedStatus}\nAction: {deniedAction}";
            if (conversationId.HasValue)
                await _conversations.SaveMessageAsync(conversationId.Value, "tool", deniedTranscript);

            await _settings.LogFileAccessAsync(request.Tool, toolPath, false);

            return new ExecutionOutcome(
                false,
                deniedResult,
                deniedStatus,
                toolPath,
                null,
                _followUpWorkflow.BuildToolResultHistoryContent(request.Tool, deniedResult));
        }
    }
}
