using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Aire.AppLayer.Api;
using Aire.AppLayer.Tools;
using Aire.Services;
using Aire.Services.Workflows;
using ChatMessage = Aire.UI.MainWindow.Models.ChatMessage;

namespace Aire;

public partial class MainWindow
{
    public async Task<ApiPendingApproval[]> ApiListPendingApprovalsAsync()
        => await DispatchAsync(async () =>
        {
            await AppStartupState.WaitUntilReadyAsync();

            var pending = new List<ApiPendingApproval>();
            for (int i = 0; i < Messages.Count; i++)
            {
                var msg = Messages[i];
                if (!msg.IsApprovalPending || msg.PendingToolCall == null || msg.ApprovalTcs == null || msg.ApprovalTcs.Task.IsCompleted)
                    continue;

                pending.Add(new ApiPendingApproval
                {
                    Index = i,
                    Tool = msg.PendingToolCall.Tool,
                    Description = msg.PendingToolCall.Description,
                    RawJson = msg.PendingToolCall.RawJson,
                    Timestamp = msg.Timestamp
                });
            }

            return pending.ToArray();
        });

    public async Task<ApiPendingApproval?> ApiGetFirstPendingApprovalAsync()
    {
        var pending = await ApiListPendingApprovalsAsync();
        return pending.FirstOrDefault();
    }

    public async Task<bool> ApiSetPendingApprovalAsync(int index, bool approved)
        => await DispatchAsync(async () =>
        {
            await AppStartupState.WaitUntilReadyAsync();

            if (index < 0 || index >= Messages.Count) return false;
            var msg = Messages[index];
            if (!msg.IsApprovalPending || msg.ApprovalTcs == null || msg.ApprovalTcs.Task.IsCompleted)
                return false;

            msg.ApprovalTcs.TrySetResult(approved);
            return true;
        });

    public Task<ApiToolExecutionResult> ApiExecuteToolAsync(string tool, JsonElement parameters)
        => ApiExecuteToolAsync(tool, parameters, waitForApproval: true, approvalTimeoutSeconds: 300);

    public async Task<ApiToolExecutionResult> ApiExecuteToolAsync(
        string tool,
        JsonElement parameters,
        bool waitForApproval,
        int approvalTimeoutSeconds)
        => await DispatchAsync(async () => await ExecuteToolCoreAsync(tool, parameters, waitForApproval, approvalTimeoutSeconds));

    private async Task<ApiToolExecutionResult> ExecuteToolCoreAsync(
        string tool,
        JsonElement parameters,
        bool waitForApproval,
        int approvalTimeoutSeconds)
    {
        await AppStartupState.WaitUntilReadyAsync();
        var apiService = _localApiApplicationService ?? new LocalApiApplicationService();
        var request = apiService.BuildToolRequest(tool, parameters);
        var normalized = request.Tool;
        bool isWindowVisible;
        try { isWindowVisible = IsVisible; }
        catch { isWindowVisible = false; }

        var autoApprove = await IsToolAutoAcceptedAsync(normalized);
        var promptService = _toolApprovalPromptApplicationService ?? new ToolApprovalPromptApplicationService();
        var promptPlan = promptService.BuildPromptPlan(autoApprove, isWindowVisible);
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var approvalMsg = new ChatMessage
        {
            Sender = "AI",
            Text = string.Empty,
            Timestamp = DateTime.Now.ToString("HH:mm"),
            MessageDate = DateTime.Now,
            BackgroundBrush = AiBgBrush,
            SenderForeground = AiFgBrush,
            PendingToolCall = request,
            IsApprovalPending = promptPlan.IsApprovalPending,
            ApprovalTcs = tcs
        };

        Messages.Add(approvalMsg);
        ScrollToBottom();

        if (promptPlan.AutoApproveImmediately)
        {
            approvalMsg.IsApprovalPending = false;
            tcs.TrySetResult(true);
        }

        var executionTask = ProcessApiToolApprovalAsync(approvalMsg, request, tcs.Task);

        var trayService = TrayService;
        if (promptPlan.ShouldRevealWindow && trayService != null)
            trayService.ShowMainWindow();

        var approvalIndex = Messages.IndexOf(approvalMsg);
        if (autoApprove)
            return await executionTask;

        if (!waitForApproval)
        {
            return apiService.BuildPendingApprovalResult(normalized, approvalIndex >= 0 ? approvalIndex : null);
        }

        approvalTimeoutSeconds = Math.Clamp(approvalTimeoutSeconds, 1, 3600);
        var completedTask = await Task.WhenAny(executionTask, Task.Delay(TimeSpan.FromSeconds(approvalTimeoutSeconds)));
        if (completedTask != executionTask)
        {
            return apiService.BuildPendingApprovalResult(normalized, approvalIndex >= 0 ? approvalIndex : null);
        }

        return await executionTask;
    }

    internal async Task<ApiToolExecutionResult> ProcessApiToolApprovalAsync(
        ChatMessage approvalMsg,
        ToolCallRequest request,
        Task<bool> approvalTask)
    {
        var approved = await approvalTask;
        approvalMsg.IsApprovalPending = false;
        var completionService = _toolApprovalExecutionApplicationService;
        if (completionService == null && _toolExecutionService != null && _databaseService != null)
        {
            completionService = new ToolApprovalExecutionApplicationService(
                _toolApprovalPromptApplicationService ?? new ToolApprovalPromptApplicationService(),
                new ToolExecutionWorkflowService(_toolExecutionService, _databaseService, _databaseService));
        }

        if (completionService == null)
        {
            if (!approved)
            {
                approvalMsg.ToolCallStatus = "\u2717 Denied";
                return new ApiToolExecutionResult
                {
                    Status = "denied",
                    TextResult = "Tool execution was denied."
                };
            }

            if (_toolExecutionService == null)
                throw new InvalidOperationException("Tool execution service is not available.");

            var directResult = await _toolExecutionService.ExecuteAsync(request);
            approvalMsg.ToolCallStatus = $"\u2713 {request.Description}";
            return (_localApiApplicationService ?? new LocalApiApplicationService())
                .BuildCompletedToolResult(directResult);
        }

        var completion = await completionService.CompleteAsync(request, approved, _currentConversationId);

        approvalMsg.ToolCallStatus = completion.ToolCallStatus;
        if (completion.Status == "denied")
        {
            return new ApiToolExecutionResult
            {
                Status = completion.Status,
                TextResult = completion.TextResult
            };
        }

        return (_localApiApplicationService ?? new LocalApiApplicationService())
            .BuildCompletedToolResult(completion.ExecutionOutcome.ExecutionResult!);
    }
}
