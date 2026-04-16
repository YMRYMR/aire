using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
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
            if (!string.Equals(mode.Key, modeKey?.Trim(), System.StringComparison.OrdinalIgnoreCase))
                return false;

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

    // ── UI state control ──────────────────────────────────────────────────────

    public Task<bool> ApiToggleSidebarAsync(bool? open = null)
        => DispatchAsync(async () =>
        {
            await AppStartupState.WaitUntilReadyAsync();
            var shouldOpen = open ?? !_sidebarOpen;
            if (shouldOpen != _sidebarOpen)
                SidebarToggleButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            return true;
        });

    public Task<bool> ApiToggleVoiceOutputAsync(bool? enabled = null)
        => DispatchAsync(() =>
        {
            var target = enabled ?? !_ttsService.VoiceEnabled;
            if (target != _ttsService.VoiceEnabled)
                VoiceFlow.ToggleVoiceOutput();
            return true;
        });

    public Task<bool> ApiOpenSearchAsync(string? query = null)
        => DispatchAsync(() =>
        {
            OpenSearch();
            if (!string.IsNullOrEmpty(query))
            {
                SearchTextBox.Text = query;
                PerformSearch(query);
            }
            return true;
        });

    public Task<bool> ApiNavigateSearchAsync(string direction)
        => DispatchAsync(() =>
        {
            if (string.Equals(direction, "prev", System.StringComparison.OrdinalIgnoreCase))
                NavigateSearchPrev();
            else
                NavigateSearchNext();
            return true;
        });

    public Task<bool> ApiCloseSearchAsync()
        => DispatchAsync(() =>
        {
            CloseSearch();
            return true;
        });

    public Task<bool> ApiPinWindowAsync(bool? pinned = null)
        => DispatchAsync(() =>
        {
            var isCurrentlyPinned = TrayService?.IsAttachedToTray ?? _isAttached;
            var target = pinned ?? !isCurrentlyPinned;
            if (target != isCurrentlyPinned)
                PinButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            return true;
        });

    // ── Orchestrator mode ────────────────────────────────────────────────────

    public Task<bool> ApiToggleOrchestratorModeAsync(bool? enabled = null, int? budget = null, List<string>? categories = null, List<string>? goals = null)
        => DispatchAsync(async () =>
        {
            await AppStartupState.WaitUntilReadyAsync();
            if (_agentModeService == null) return false;

            var isActive = _agentModeService.IsActive;
            var target = enabled ?? !isActive;

            if (target && !isActive)
            {
                var config = new UI.AgentModeConfigDialog.OrchestratorConfig(
                    budget ?? 0,
                    goals ?? new List<string>(),
                    categories != null && categories.Count > 0
                        ? new System.Collections.Generic.HashSet<string>(categories, System.StringComparer.OrdinalIgnoreCase)
                        : new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase));
                return await StartOrchestratorModeAsync(config);
            }
            else if (!target && isActive)
            {
                StopOrchestratorMode("user stopped");
            }
            return true;
        });

    public Task<bool> ApiSetOrchestratorGoalsAsync(List<string> goals)
        => DispatchAsync(async () =>
        {
            await AppStartupState.WaitUntilReadyAsync();
            if (_agentModeService == null) return false;
            _agentModeService.SetGoals(goals);
            PersistOrchestratorConfigSnapshot(_agentModeService.TokenBudget, _agentModeService.Goals, _agentModeService.AllowedCategories);
            if (_agentModeService.IsActive)
            {
                await AddSystemMessageAsync(LocalizationService.S(
                    "orchestrator.goalsUpdated",
                    "I updated the Orchestrator goals and will continue from this new list."));
                PersistOrchestratorSessionSnapshot();
            }
            return true;
        });

    public Task<bool> ApiToggleAgentModeAsync(bool? enabled = null, int? budget = null, List<string>? categories = null)
        => ApiToggleOrchestratorModeAsync(enabled, budget, categories, null);

    // ── Composer ──────────────────────────────────────────────────────────────

    public Task<bool> ApiSetInputAsync(string text)
        => DispatchAsync(() =>
        {
            InputTextBox.Text = text ?? string.Empty;
            InputTextBox.CaretIndex = InputTextBox.Text.Length;
            return true;
        });

    public Task<bool> ApiAttachFileAsync(string filePath)
        => DispatchAsync(() =>
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return false;
            AttachFileOrImage(filePath);
            return true;
        });

    public Task<bool> ApiRemoveAttachmentAsync()
        => DispatchAsync(() =>
        {
            RemoveImageButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            return true;
        });

    public async Task<int> ApiBranchConversationAsync(int conversationId, int upToMessageId)
        => await DispatchAsync(async () =>
        {
            await AppStartupState.WaitUntilReadyAsync();
            var newId = await _databaseService.BranchConversationAsync(conversationId, upToMessageId);
            _currentConversationId = newId;
            await LoadConversationMessages(newId, syncProviderSelection: true);
            if (_sidebarOpen)
                await RefreshSidebarAsync();
            return newId;
        });

    // ── Conversation ──────────────────────────────────────────────────────────

    public Task<bool> ApiBranchFromMessageAsync(int messageId)
        => DispatchAsync(async () =>
        {
            await AppStartupState.WaitUntilReadyAsync();
            if (!_currentConversationId.HasValue) return false;
            await ConversationFlow.BranchFromMessageAsync(messageId);
            return true;
        });
}
