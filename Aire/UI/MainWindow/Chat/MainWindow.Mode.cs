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

        internal string BuildAssistantModePrompt()
            => _assistantModeApplicationService.BuildPromptSection(_assistantModeKey);

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
                AddSystemMessage(string.Format(
                    LocalizationService.S("main.assistantModeSwitched", "Assistant mode switched to {0}."),
                    mode.DisplayName));
                if (_sidebarOpen)
                    await RefreshSidebarAsync();
            }
        }
    }
}
