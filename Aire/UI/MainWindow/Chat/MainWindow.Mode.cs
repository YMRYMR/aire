using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using ContextMenu = System.Windows.Controls.ContextMenu;
using MenuItem = System.Windows.Controls.MenuItem;
using Aire.AppLayer.Chat;
using Aire.Services;

namespace Aire
{
    public partial class MainWindow
    {
        private ContextMenu? _modeMenu;

        private void ModeButton_Click(object sender, RoutedEventArgs e)
        {
            EnsureModeMenu();
            _modeMenu!.PlacementTarget = ModeButton;
            _modeMenu.IsOpen = true;
        }

        internal void UpdateModeButtonState()
        {
            if (ModeButton == null)
                return;

            ModeButton.Content = _assistantModeDisplayName;
            ModeButton.ToolTip = string.Format(
                LocalizationService.S("tooltip.modeCurrent", "Assistant mode: {0}"),
                _assistantModeDisplayName);
        }

        internal string BuildAssistantModePrompt(bool recoveryTurn = false)
        {
            var assistant = _assistantModeApplicationService.BuildPromptSection(_assistantModeKey);
            var orchestrator = recoveryTurn
                ? BuildOrchestratorRecoveryModePromptSection()
                : BuildOrchestratorModePromptSection();
            if (string.IsNullOrWhiteSpace(orchestrator))
                return assistant;

            return string.IsNullOrWhiteSpace(assistant)
                ? orchestrator
                : $"{assistant}\n\n{orchestrator}";
        }

        internal string BuildOrchestratorModePromptSection()
        {
            if (_agentModeService == null || !_agentModeService.IsActive)
                return string.Empty;

            var goals = _agentModeService.Goals.Count == 0
                ? "No goals currently set."
                : string.Join("\n", _agentModeService.Goals.Select(goal => $"• {goal}"));

            var categories = _agentModeService.AllowedCategories == null || _agentModeService.AllowedCategories.Count == 0
                ? "All tool categories are allowed."
                : $"Allowed tool categories: {string.Join(", ", _agentModeService.AllowedCategories.OrderBy(c => c))}.";

            var stopReason = string.IsNullOrWhiteSpace(_agentModeService.StopReason)
                ? "Continue until the goals are complete, the user stops the mode, or you become blocked."
                : $"Last stop reason: {_agentModeService.StopReason}.";

            return
                "ORCHESTRATOR MODE:\n" +
                "You are running an autonomous goal-driven session. Work through the goals below, use allowed tools automatically, and keep the user informed in plain language.\n" +
                "Before a meaningful action, briefly explain what you are going to do and why.\n" +
                "Treat new user messages as steering input and adapt the plan when the user adds clarification.\n" +
                $"Goals:\n{goals}\n" +
                $"{categories}\n" +
                $"{stopReason}\n" +
                "If a goal is completed, say so clearly and move to the next one. If you are blocked after repeated failures, ask the user for guidance.\n" +
                "If the session was resumed after a stop or crash, continue from the saved checkpoint instead of restarting from scratch.";
        }

        internal string BuildOrchestratorRecoveryModePromptSection()
        {
            if (_agentModeService == null || !_agentModeService.IsActive)
                return string.Empty;

            return
                "ORCHESTRATOR RECOVERY:\n" +
                "Continue from the interrupted step using the smallest possible reply.\n" +
                "If you need a tool, emit only the next tool call.\n" +
                "Do not repeat prior narration or restate the goals unless it is necessary to resume safely.";
        }

        internal void ApplyAssistantModeState(string? key)
        {
            var mode = _assistantModeApplicationService.ResolveMode(key);
            _assistantModeKey = mode.Key;
            _assistantModeDisplayName = mode.DisplayName;
            UpdateModeButtonState();
            SyncModeMenuChecks();
        }

        private void EnsureModeMenu()
        {
            if (_modeMenu == null)
                _modeMenu = new ContextMenu();

            _modeMenu.Items.Clear();
            foreach (var mode in _assistantModeApplicationService.GetModes())
            {
                var item = new MenuItem
                {
                    Header = mode.DisplayName,
                    ToolTip = mode.Description,
                    IsCheckable = true,
                    Tag = mode.Key
                };
                item.Click += async (_, _) => await ApplyAssistantModeAsync(mode);
                _modeMenu.Items.Add(item);
            }

            SyncModeMenuChecks();
        }

        private void SyncModeMenuChecks()
        {
            if (_modeMenu == null)
                return;

            foreach (var item in _modeMenu.Items.OfType<MenuItem>())
                item.IsChecked = string.Equals(item.Tag as string, _assistantModeKey, StringComparison.OrdinalIgnoreCase);
        }

        private async Task ApplyAssistantModeAsync(AssistantModeApplicationService.AssistantModeDefinition mode)
        {
            ApplyAssistantModeState(mode.Key);

            if (_currentConversationId.HasValue)
            {
                await _conversationApplicationService.UpdateConversationAssistantModeAsync(_currentConversationId.Value, mode.Key);
                await AddSystemMessageAsync(string.Format(
                    LocalizationService.S("main.assistantModeSwitched", "Assistant mode switched to {0}."),
                    mode.DisplayName));
                if (_sidebarOpen)
                    await RefreshSidebarAsync();
            }
        }
    }
}
