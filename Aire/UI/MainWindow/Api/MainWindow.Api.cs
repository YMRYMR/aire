using System.Text.Json;
using Aire.AppLayer.Tools;
using Aire.AppLayer.Api;
using Aire.Data;
using Aire.AppLayer.Providers;
using Aire.Domain.Providers;
using Aire.Services;
using Aire.Services.Providers;
using ChatMessage = Aire.UI.MainWindow.Models.ChatMessage;

namespace Aire
{
    public partial class MainWindow
    {
        /// <summary>
        /// Marshals an async operation to the WPF Dispatcher thread and awaits the result.
        /// </summary>
        private async Task<T> DispatchAsync<T>(Func<Task<T>> action)
        {
            var op = Dispatcher.InvokeAsync(action);
            var task = await op.Task.ConfigureAwait(false);
            return await task.ConfigureAwait(false);
        }

        /// <summary>
        /// Marshals a synchronous operation to the WPF Dispatcher thread and awaits the result.
        /// </summary>
        private async Task<T> DispatchAsync<T>(Func<T> action)
        {
            var op = Dispatcher.InvokeAsync(action);
            return await op.Task.ConfigureAwait(false);
        }

        public async Task<List<ApiProviderSnapshot>> ApiListProvidersAsync()
            => await DispatchAsync(async () =>
            {
                await AppStartupState.WaitUntilReadyAsync();
                var providers = await _providerFactory.GetConfiguredProvidersAsync();
                return (_localApiApplicationService ?? new Aire.AppLayer.Api.LocalApiApplicationService())
                    .BuildProviderSnapshots(providers);
            });

        public async Task<List<ConversationSummary>> ApiListConversationsAsync(string? search = null)
            => await DispatchAsync(async () =>
            {
                await AppStartupState.WaitUntilReadyAsync();
                return await _conversationApplicationService.ListConversationsAsync(search);
            });

        public async Task<List<Aire.Data.Message>> ApiGetMessagesAsync(int conversationId)
            => await DispatchAsync(async () =>
            {
                await AppStartupState.WaitUntilReadyAsync();
                return await _conversationApplicationService.GetMessagesAsync(conversationId);
            });

        public async Task<int> ApiCreateConversationAsync(string? title = null, int? providerId = null)
            => await DispatchAsync(async () =>
            {
                await AppStartupState.WaitUntilReadyAsync();

                Provider sel;
                if (providerId.HasValue)
                {
                    // Switch provider directly without reassigning any existing conversation.
                    sel = ProviderComboBox.Items.OfType<Provider>()
                        .FirstOrDefault(p => p.Id == providerId.Value)
                        ?? throw new InvalidOperationException($"Provider with ID {providerId.Value} not found.");

                    _suppressProviderChange = true;
                    try
                    {
                        try { _currentProvider = _providerFactory.CreateProvider(sel); }
                        catch { _currentProvider = null; }

                        ProviderComboBox.SelectedItem = sel;
                        _currentProviderId = sel.Id;
                        await _chatService.SetProviderAsync(sel.Id);
                        await _chatSessionApplicationService.SaveSelectedProviderAsync(sel.Id);
                        UpdateCapabilityUI();
                    }
                    finally
                    {
                        _suppressProviderChange = false;
                    }
                }
                else
                {
                    sel = ProviderComboBox.SelectedItem as Provider
                        ?? throw new InvalidOperationException("No provider is selected.");
                }

                var plan = (_localApiApplicationService ?? new Aire.AppLayer.Api.LocalApiApplicationService())
                    .BuildConversationCreationPlan(sel.Name, title);
                var id = await ConversationFlow.CreateConversationAsync(sel, plan.Title, plan.SystemMessage);
                if (_sidebarOpen)
                    await RefreshSidebarAsync();
                return id;
            });

        public async Task<bool> ApiSelectConversationAsync(int conversationId)
            => await DispatchAsync(async () =>
            {
                await AppStartupState.WaitUntilReadyAsync();
                var conv = await _conversationApplicationService.GetConversationAsync(conversationId);
                if (conv == null) return false;

                _currentConversationId = conversationId;
                await LoadConversationMessages(conversationId, syncProviderSelection: false);
                if (_sidebarOpen)
                    await RefreshSidebarAsync();
                return true;
            });

        public async Task<bool> ApiSetProviderAsync(int providerId)
            => await DispatchAsync(async () =>
            {
                await AppStartupState.WaitUntilReadyAsync();
                var provider = ProviderComboBox.Items.OfType<Provider>()
                    .FirstOrDefault(p => p.Id == providerId);
                if (provider == null) return false;

                _suppressProviderChange = true;
                try
                {
                    ProviderComboBox.SelectedItem = provider;
                    await UpdateCurrentProvider(showSwitchedMessage: false);
                }
                finally
                {
                    _suppressProviderChange = false;
                }
                return true;
            });

        public Task<ApiStateSnapshot> ApiGetStateAsync()
            => DispatchAsync(() =>
            {
                var provider = ProviderComboBox.SelectedItem as Provider;
                var selectedWindow = WindowCaptureService.GetSelectedWindow();
                var pendingCount = Messages.Count(m => m.IsApprovalPending && m.ApprovalTcs != null && !m.ApprovalTcs.Task.IsCompleted);

                return (_localApiApplicationService ?? new Aire.AppLayer.Api.LocalApiApplicationService())
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
                            selectedWindow,
                            pendingCount);
            });
    }
}
