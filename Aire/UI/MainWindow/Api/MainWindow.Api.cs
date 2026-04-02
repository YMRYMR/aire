using System.Text.Json;
using Aire.AppLayer.Tools;
using Aire.Data;
using Aire.Services;
using ChatMessage = Aire.UI.MainWindow.Models.ChatMessage;

namespace Aire
{
    public partial class MainWindow
    {
        public async Task<List<ApiProviderSnapshot>> ApiListProvidersAsync()
        {
            await AppStartupState.WaitUntilReadyAsync();
            var providers = await _providerFactory.GetConfiguredProvidersAsync();
            return (_localApiApplicationService ?? new Aire.AppLayer.Api.LocalApiApplicationService())
                .BuildProviderSnapshots(providers);
        }

        public async Task<List<ConversationSummary>> ApiListConversationsAsync(string? search = null)
        {
            await AppStartupState.WaitUntilReadyAsync();
            return await _conversationApplicationService.ListConversationsAsync(search);
        }

        public async Task<List<Aire.Data.Message>> ApiGetMessagesAsync(int conversationId)
        {
            await AppStartupState.WaitUntilReadyAsync();
            return await _conversationApplicationService.GetMessagesAsync(conversationId);
        }

        public async Task<int> ApiCreateConversationAsync(string? title = null)
        {
            await AppStartupState.WaitUntilReadyAsync();
            var sel = ProviderComboBox.SelectedItem as Provider;
            if (sel == null)
                throw new InvalidOperationException("No provider is selected.");

            var plan = (_localApiApplicationService ?? new Aire.AppLayer.Api.LocalApiApplicationService())
                .BuildConversationCreationPlan(sel.Name, title);
            var id = await ConversationFlow.CreateConversationAsync(sel, plan.Title, plan.SystemMessage);
            if (_sidebarOpen)
                await RefreshSidebarAsync();
            return id;
        }

        public async Task<bool> ApiSelectConversationAsync(int conversationId)
        {
            await AppStartupState.WaitUntilReadyAsync();
            var conv = await _conversationApplicationService.GetConversationAsync(conversationId);
            if (conv == null) return false;

            _currentConversationId = conversationId;
            await LoadConversationMessages(conversationId);
            if (_sidebarOpen)
                await RefreshSidebarAsync();
            return true;
        }

        public async Task<bool> ApiSetProviderAsync(int providerId)
        {
            await AppStartupState.WaitUntilReadyAsync();
            var provider = ProviderComboBox.Items.OfType<Provider>()
                .FirstOrDefault(p => p.Id == providerId);
            if (provider == null) return false;

            _suppressProviderChange = true;
            ProviderComboBox.SelectedItem = provider;
            _suppressProviderChange = false;
            await UpdateCurrentProvider(showSwitchedMessage: false);
            return true;
        }

        public async Task<bool> ApiSetProviderModelAsync(int providerId, string model)
        {
            await AppStartupState.WaitUntilReadyAsync();
            var provider = ProviderComboBox.Items.OfType<Provider>()
                .FirstOrDefault(p => p.Id == providerId);
            if (provider == null) return false;

            var normalizedModel = (_localApiApplicationService ?? new Aire.AppLayer.Api.LocalApiApplicationService())
                .NormalizeProviderModel(model);
            if (normalizedModel == null)
                return false;

            provider.Model = normalizedModel;
            await _providerFactory.UpdateProviderAsync(provider);

            if (providerId == _currentProviderId)
                await UpdateCurrentProvider(showSwitchedMessage: false);
            else
                await RefreshProvidersAsync();

            return true;
        }

        public async Task<ApiPendingApproval[]> ApiListPendingApprovalsAsync()
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
        }

        public async Task<bool> ApiSetPendingApprovalAsync(int index, bool approved)
        {
            await AppStartupState.WaitUntilReadyAsync();

            if (index < 0 || index >= Messages.Count) return false;
            var msg = Messages[index];
            if (!msg.IsApprovalPending || msg.ApprovalTcs == null || msg.ApprovalTcs.Task.IsCompleted)
                return false;

            msg.ApprovalTcs.TrySetResult(approved);
            return true;
        }

        public async Task<ApiToolExecutionResult> ApiExecuteToolAsync(string tool, JsonElement parameters)
            => await ApiExecuteToolAsync(tool, parameters, waitForApproval: true, approvalTimeoutSeconds: 300);

        public async Task<ApiToolExecutionResult> ApiExecuteToolAsync(
            string tool,
            JsonElement parameters,
            bool waitForApproval,
            int approvalTimeoutSeconds)
        {
            await AppStartupState.WaitUntilReadyAsync();
            var apiService = _localApiApplicationService ?? new Aire.AppLayer.Api.LocalApiApplicationService();
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
                    new Services.Workflows.ToolExecutionWorkflowService(_toolExecutionService, _databaseService, _databaseService));
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
                return (_localApiApplicationService ?? new Aire.AppLayer.Api.LocalApiApplicationService())
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

            return (_localApiApplicationService ?? new Aire.AppLayer.Api.LocalApiApplicationService())
                .BuildCompletedToolResult(completion.ExecutionOutcome.ExecutionResult!);
        }

        public Task<ApiStateSnapshot> ApiGetStateAsync()
        {
            var provider = ProviderComboBox.SelectedItem as Provider;
            var pendingCount = Messages.Count(m => m.IsApprovalPending && m.ApprovalTcs != null && !m.ApprovalTcs.Task.IsCompleted);

            return Task.FromResult((_localApiApplicationService ?? new Aire.AppLayer.Api.LocalApiApplicationService())
                .BuildStateSnapshot(
                    LocalApiService.Port,
                    AppStartupState.IsReady,
                    IsVisible,
                    _settingsWindow != null,
                    UI.WebViewWindow.Current != null,
                    AppState.GetApiAccessEnabled(),
                    !string.IsNullOrWhiteSpace(AppState.GetApiAccessToken()),
                    _currentConversationId,
                    provider,
                    pendingCount));
        }
    }
}
