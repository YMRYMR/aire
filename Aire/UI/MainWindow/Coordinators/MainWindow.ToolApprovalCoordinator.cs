using System;
using System.Threading.Tasks;
using Aire.AppLayer.Tools;
using Aire.Services;
using ChatMessage = Aire.UI.MainWindow.Models.ChatMessage;

namespace Aire
{
    public partial class MainWindow
    {
        private ToolApprovalCoordinator? _toolApprovalCoordinator;
        private ToolApprovalCoordinator ToolApprovals => _toolApprovalCoordinator ??= new ToolApprovalCoordinator(this);

        /// <summary>
        /// Owns approval prompts and short-lived input-session state for tool execution.
        /// </summary>
        private sealed class ToolApprovalCoordinator
        {
            private readonly MainWindow _owner;

            public ToolApprovalCoordinator(MainWindow owner)
            {
                _owner = owner;
            }

            /// <summary>
            /// Decides whether a tool call can run immediately based on active sessions or saved auto-accept policy.
            /// </summary>
            /// <param name="toolName">Canonical tool name requested by the model.</param>
            /// <returns><see langword="true"/> when the tool should run without showing an approval prompt.</returns>
            public async Task<bool> DetermineAutoApproveAsync(string toolName)
            {
                var sessionService = GetSessionService();
                var decision = await sessionService.DetermineAutoApproveAsync(toolName, DateTime.Now);
                if (!string.IsNullOrWhiteSpace(decision.SessionStatusMessage))
                    await _owner.AddSystemMessageAsync(decision.SessionStatusMessage);
                _owner.UpdateMouseSessionBanner();
                return decision.AutoApprove;
            }

            /// <summary>
            /// Creates the approval message shown in chat and waits for a user decision when auto-approval is not allowed.
            /// </summary>
            /// <param name="parsed">Parsed AI response containing the pending tool call.</param>
            /// <param name="autoApprove">Whether the tool can run immediately without waiting for the user.</param>
            /// <returns>The approval decision together with the chat message that tracks its UI state.</returns>
            public async Task<(bool approved, ChatMessage approvalMsg)> RequestApprovalAsync(ParsedAiResponse parsed, bool autoApprove)
                => await RequestApprovalAsync(parsed.ToolCall!, autoApprove);

            /// <summary>
            /// Creates the approval message shown in chat and waits for a user decision when auto-approval is not allowed.
            /// </summary>
            /// <param name="toolCall">Pending tool call.</param>
            /// <param name="autoApprove">Whether the tool can run immediately without waiting for the user.</param>
            /// <returns>The approval decision together with the chat message that tracks its UI state.</returns>
            public async Task<(bool approved, ChatMessage approvalMsg)> RequestApprovalAsync(ToolCallRequest toolCall, bool autoApprove)
            {
                var promptPlan = _owner._toolApprovalPromptApplicationService.BuildPromptPlan(autoApprove, _owner.IsVisible);
                var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var approvalMsg = new ChatMessage
                {
                    Sender = "AI",
                    Text = string.Empty,
                    Timestamp = DateTime.Now.ToString("HH:mm"),
                    MessageDate = DateTime.Now,
                    BackgroundBrush = MainWindow.AiBgBrush,
                    SenderForeground = MainWindow.AiFgBrush,
                    PendingToolCall = toolCall,
                    IsApprovalPending = promptPlan.IsApprovalPending,
                    ApprovalTcs = tcs
                };
                _owner.Messages.Add(approvalMsg);
                _owner.ScrollToBottom();

                bool approved;
                if (promptPlan.AutoApproveImmediately)
                {
                    tcs.SetResult(true);
                    approved = true;
                    approvalMsg.IsApprovalPending = false;
                }
                else
                {
                    if (promptPlan.ShouldRevealWindow && _owner.TrayService != null)
                        _owner.TrayService.ShowMainWindow();

                    approved = await tcs.Task;
                    approvalMsg.IsApprovalPending = false;
                }

                return (approved, approvalMsg);
            }

            /// <summary>
            /// Updates the in-memory keyboard and mouse session flags after a session-management tool executes.
            /// </summary>
            /// <param name="request">Executed tool request.</param>
            public void ApplySessionState(ToolCallRequest request)
            {
                GetSessionService().ApplyToolRequest(request, DateTime.Now);
                _owner.UpdateMouseSessionBanner();
            }

            /// <summary>
            /// Persists a screenshot result to the conversation storage area and adds it to the chat UI.
            /// </summary>
            /// <param name="execResult">Tool execution result that may contain a screenshot path.</param>
            public async Task PersistScreenshotAsync(ToolExecutionResult execResult)
            {
                if (execResult.ScreenshotPath == null)
                    return;

                var shotNow = DateTime.Now;
                try
                {
                    var persistedPath = await _owner._conversationAssetApplicationService.PersistScreenshotAsync(
                        _owner._currentConversationId,
                        execResult.ScreenshotPath,
                        MainWindow.GetScreenshotsFolder(),
                        shotNow);

                    var uri = new Uri(persistedPath);
                    var bitmap = new System.Windows.Media.Imaging.BitmapImage(uri);
                    bitmap.Freeze();
                    _owner.AddToUI(new ChatMessage
                    {
                        Sender = "AI",
                        Text = string.Empty,
                        ScreenshotImage = bitmap,
                        Timestamp = shotNow.ToString("HH:mm"),
                        MessageDate = shotNow,
                        BackgroundBrush = MainWindow.AiBgBrush,
                        SenderForeground = MainWindow.AiFgBrush
                    });
                }
                catch (Exception ex)
                {
                    AppLogger.Warn("ToolApprovalCoordinator.ShowScreenshot", "Failed to load screenshot for tool result", ex);
                }
            }

            private ToolControlSessionApplicationService GetSessionService()
                => _owner._toolControlSessionApplicationService
                ?? new ToolControlSessionApplicationService(
                    _owner._toolApprovalApplicationService
                    ?? new ToolApprovalApplicationService(
                        new Aire.Services.Policies.ToolAutoAcceptPolicyService(() => Task.FromResult<string?>(UI.SettingsWindow.AutoAcceptJsonCache))));
        }
    }
}
