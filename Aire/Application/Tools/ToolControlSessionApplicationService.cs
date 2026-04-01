using System;
using System.Threading.Tasks;
using Aire.Services;

namespace Aire.AppLayer.Tools
{
    /// <summary>
    /// Owns the short-lived mouse and keyboard control session state used by tool approvals.
    /// This keeps session rules and expiry handling out of the window layer.
    /// </summary>
    public sealed class ToolControlSessionApplicationService
    {
        /// <summary>
        /// UI-facing banner state for the current control session, if any.
        /// </summary>
        /// <param name="IsVisible">Whether a session banner should be shown.</param>
        /// <param name="BannerText">User-facing status text for the active session.</param>
        /// <param name="SessionActive">Whether any control session is still active.</param>
        public sealed record SessionBannerPlan(
            bool IsVisible,
            string? BannerText,
            bool SessionActive);

        private readonly ToolApprovalApplicationService _approvalService;
        private ToolApprovalSessionState _sessionState = new(false, default, false, default);

        /// <summary>
        /// Creates the session owner over the existing approval/session rule engine.
        /// </summary>
        /// <param name="approvalService">Service that evaluates tool sessions and auto-approval rules.</param>
        public ToolControlSessionApplicationService(ToolApprovalApplicationService approvalService)
        {
            _approvalService = approvalService;
        }

        /// <summary>
        /// Evaluates whether a tool can run immediately and updates session state if an existing session expired.
        /// </summary>
        /// <param name="toolName">Canonical or aliased tool name requested by the model.</param>
        /// <param name="now">Current time used to evaluate session expiry.</param>
        /// <returns>The normalized approval decision after applying any session-state changes.</returns>
        public async Task<ToolApprovalApplicationService.ApprovalDecision> DetermineAutoApproveAsync(string toolName, DateTime now)
        {
            var decision = await _approvalService.DetermineAutoApproveAsync(toolName, _sessionState, now);
            _sessionState = decision.SessionState;
            return decision;
        }

        /// <summary>
        /// Applies the result of a session-management tool to the active control-session state.
        /// </summary>
        /// <param name="request">Executed tool request that may start or stop a control session.</param>
        /// <param name="now">Current time used to compute new session expiries.</param>
        public void ApplyToolRequest(ToolCallRequest request, DateTime now)
        {
            _sessionState = _approvalService.ApplySessionState(request, _sessionState, now);
        }

        /// <summary>
        /// Stops all active control sessions immediately.
        /// </summary>
        public void Stop()
        {
            _sessionState = _sessionState with
            {
                MouseSessionActive = false,
                KeyboardSessionActive = false
            };
        }

        /// <summary>
        /// Builds the current banner state and clears expired sessions before returning it.
        /// </summary>
        /// <param name="now">Current time used to evaluate session expiry.</param>
        /// <returns>Banner state describing whether a control session should be shown in the UI.</returns>
        public SessionBannerPlan BuildBannerPlan(DateTime now)
        {
            if (_sessionState.MouseSessionActive && now < _sessionState.MouseSessionExpiry)
            {
                var remaining = (int)Math.Ceiling((_sessionState.MouseSessionExpiry - now).TotalMinutes);
                return new SessionBannerPlan(true, $"Mouse session active \u2014 expires in ~{remaining} min", SessionActive: true);
            }

            if (_sessionState.KeyboardSessionActive && now < _sessionState.KeyboardSessionExpiry)
            {
                var remaining = (int)Math.Ceiling((_sessionState.KeyboardSessionExpiry - now).TotalMinutes);
                return new SessionBannerPlan(true, $"Keyboard session active \u2014 expires in ~{remaining} min", SessionActive: true);
            }

            _sessionState = _sessionState with
            {
                MouseSessionActive = false,
                KeyboardSessionActive = false
            };
            return new SessionBannerPlan(false, null, SessionActive: false);
        }
    }
}
