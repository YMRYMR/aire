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
            await LoadConversationMessages(conversationId, syncProviderSelection: false);
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
        }

    public Task<ApiStateSnapshot> ApiGetStateAsync()
    {
        var provider = ProviderComboBox.SelectedItem as Provider;
        var selectedWindow = WindowCaptureService.GetSelectedWindow();
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
                    selectedWindow,
                    pendingCount));
    }
}
}
