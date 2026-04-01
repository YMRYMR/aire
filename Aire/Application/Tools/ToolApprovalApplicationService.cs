using System;
using System.Text.Json;
using System.Threading.Tasks;
using Aire.Services;
using Aire.Services.Policies;

namespace Aire.AppLayer.Tools
{
    /// <summary>
    /// Application-layer rules for tool auto-approval and temporary mouse/keyboard control sessions.
    /// </summary>
    public sealed class ToolApprovalApplicationService
    {
        /// <summary>
        /// Result of evaluating whether a tool can run without prompting the user.
        /// </summary>
        public sealed record ApprovalDecision(
            bool AutoApprove,
            ToolApprovalSessionState SessionState,
            string? SessionStatusMessage = null);

        private readonly ToolAutoAcceptPolicyService _policyService;

        /// <summary>
        /// Creates the approval workflow over the persisted auto-accept policy boundary.
        /// </summary>
        /// <param name="policyService">Policy service that evaluates saved auto-accept settings.</param>
        public ToolApprovalApplicationService(ToolAutoAcceptPolicyService policyService)
        {
            _policyService = policyService;
        }

        /// <summary>
        /// Determines whether the requested tool can run immediately from an active session or saved policy.
        /// </summary>
        public async Task<ApprovalDecision> DetermineAutoApproveAsync(
            string toolName,
            ToolApprovalSessionState sessionState,
            DateTime now)
        {
            toolName = ToolExecutionService.NormalizeToolName(toolName);
            bool autoApprove = false;
            string? sessionStatusMessage = null;
            var updatedState = sessionState;
            bool isKeyboardTool = IsKeyboardTool(toolName);
            bool isMouseTool = IsMouseTool(toolName);

            if (toolName != "begin_keyboard_session" && toolName != "begin_mouse_session")
            {
                if (isKeyboardTool)
                {
                    if (sessionState.KeyboardSessionActive && now < sessionState.KeyboardSessionExpiry)
                    {
                        autoApprove = true;
                    }
                    else if (sessionState.MouseSessionActive && now < sessionState.MouseSessionExpiry)
                    {
                        autoApprove = true;
                    }
                    else if (sessionState.KeyboardSessionActive)
                    {
                        updatedState = updatedState with { KeyboardSessionActive = false };
                        sessionStatusMessage = "Keyboard session expired.";
                    }
                }
                else if (isMouseTool)
                {
                    if (sessionState.MouseSessionActive && now < sessionState.MouseSessionExpiry)
                    {
                        autoApprove = true;
                    }
                    else if (sessionState.MouseSessionActive)
                    {
                        updatedState = updatedState with { MouseSessionActive = false };
                        sessionStatusMessage = "Mouse session expired.";
                    }
                }
            }

            if (!autoApprove)
                autoApprove = await _policyService.IsAutoAcceptedAsync(toolName);

            return new ApprovalDecision(autoApprove, updatedState, sessionStatusMessage);
        }

        /// <summary>
        /// Applies the side effects of a session-management tool to the current session state.
        /// </summary>
        public ToolApprovalSessionState ApplySessionState(
            ToolCallRequest request,
            ToolApprovalSessionState sessionState,
            DateTime now)
        {
            var toolName = ToolExecutionService.NormalizeToolName(request.Tool);

            return toolName switch
            {
                "begin_keyboard_session" => sessionState with
                {
                    KeyboardSessionActive = true,
                    KeyboardSessionExpiry = now.AddMinutes(ReadDurationMinutes(request.Parameters, defaultMinutes: 10))
                },
                "end_keyboard_session" => sessionState with
                {
                    KeyboardSessionActive = false
                },
                "begin_mouse_session" => sessionState with
                {
                    MouseSessionActive = true,
                    MouseSessionExpiry = now.AddMinutes(ReadDurationMinutes(request.Parameters, defaultMinutes: 5))
                },
                "end_mouse_session" => sessionState with
                {
                    MouseSessionActive = false
                },
                _ => sessionState
            };
        }

        private static int ReadDurationMinutes(JsonElement parameters, int defaultMinutes)
        {
            if (parameters.TryGetProperty("duration_minutes", out var durationElement) &&
                durationElement.ValueKind == JsonValueKind.Number)
            {
                int duration = durationElement.GetInt32();
                return duration > 0 ? duration : defaultMinutes;
            }

            return defaultMinutes;
        }

        private static bool IsMouseTool(string toolName)
            => ToolExecutionService.IsMouseTool(toolName)
            || toolName is "move_mouse" or "click" or "scroll";

        private static bool IsKeyboardTool(string toolName)
            => ToolExecutionService.IsKeyboardTool(toolName)
            || toolName == "type_text";
    }
}
