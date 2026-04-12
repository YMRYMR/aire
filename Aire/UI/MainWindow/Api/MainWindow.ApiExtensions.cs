using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aire.Domain.Tools;
using Aire.Services;

namespace Aire;

public partial class MainWindow
{
    public async Task<bool> ApiSetAssistantModeAsync(string modeKey)
        => await DispatchAsync(async () =>
        {
            await AppStartupState.WaitUntilReadyAsync();
            var mode = _assistantModeApplicationService.ResolveMode(modeKey);
            ApplyAssistantModeState(mode.Key);

            if (_currentConversationId.HasValue)
            {
                await _conversationApplicationService.UpdateConversationAssistantModeAsync(
                    _currentConversationId.Value, mode.Key);
            }

            return true;
        });

    public async Task<List<string>> ApiListAssistantModesAsync()
        => await DispatchAsync(() =>
        {
            return _assistantModeApplicationService.GetModes()
                .Select(m => m.Key)
                .ToList();
        });

    public async Task<List<string>> ApiListToolCategoriesAsync()
        => await DispatchAsync(() =>
        {
            return ToolCategoryCatalog.Options.Select(o => o.Id).ToList();
        });

    public async Task<bool> ApiSetToolCategoriesAsync(List<string>? categories)
        => await DispatchAsync(async () =>
        {
            await AppStartupState.WaitUntilReadyAsync();
            _enabledToolCategories = ToolCategoryCatalog.Normalize(categories);
            await PersistToolCategoriesAsync();
            return true;
        });

    public async Task<bool> ApiDeleteConversationAsync(int conversationId)
        => await DispatchAsync(async () =>
        {
            await AppStartupState.WaitUntilReadyAsync();
            await ConversationFlow.DeleteConversationAsync(conversationId);
            if (_sidebarOpen)
                await RefreshSidebarAsync();
            return true;
        });

    public async Task<bool> ApiRenameConversationAsync(int conversationId, string title)
        => await DispatchAsync(async () =>
        {
            await AppStartupState.WaitUntilReadyAsync();
            await _conversationApplicationService.RenameConversationAsync(conversationId, title);
            if (_sidebarOpen)
                await RefreshSidebarAsync();
            return true;
        });

    public Task StopAiAsync()
        => DispatchAsync(() =>
        {
            _aiCancellationTokenSource?.Cancel();
            return true;
        });
}
