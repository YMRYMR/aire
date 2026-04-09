using System.Windows;
using System.Windows.Controls;
using Aire.Services;

namespace Aire
{
    public partial class MainWindow
    {
        private void ApplyLocalization()
        {
            var L = LocalizationService.S;
            HelpButton.ToolTip = L("tooltip.help", "Help");
            SettingsButton.ToolTip = L("tooltip.settings", "Settings");
            BrowserButton.ToolTip = L("tooltip.browser", "Open browser  (AI can read open tabs)");
            SearchButton.ToolTip = L("tooltip.searchChat", "Find in chat  (Ctrl+F)");
            ModeButton.ToolTip = L("tooltip.mode", "Assistant mode");
            MouseSessionLabel.Text = L("main.sessionActive", "Session active");
            EndSessionButton.Content = L("main.endSession", "End session");
            ThinkingText.Text = L("main.thinking", "Thinking\u2026");
            RemoveImageButton.Content = L("main.remove", "Remove");
            RemoveImageButton.ToolTip = L("tooltip.removeAttachment", "Remove attachment");
            StopAiButton.ToolTip = L("tooltip.stopThinking", "Stop AI thinking");
            SidebarToggleButton.ToolTip = L("tooltip.sidebar", "Conversation history");
            CheckAgainButton.ToolTip = L("tooltip.checkAvailability", "Check if provider is available again");
            ConversationSidebar.ToolTip = null;
            ConversationSidebar.NewConversationButtonToolTip = L("tooltip.newConversation", "New conversation");
            SearchPrevButton.ToolTip = L("tooltip.previousMatch", "Previous match");
            SearchNextButton.ToolTip = L("tooltip.nextMatch", "Next match");
            CloseSearchButton.ToolTip = L("tooltip.close", "Close");
            FileChipBorder.ToolTip = L("tooltip.openFile", "Click to open file");
            // Context menu items
            if (MessagesScrollViewer?.ContextMenu is System.Windows.Controls.ContextMenu contextMenu)
            {
                var items = contextMenu.Items;
                // Order: Find, Separator, Save, Copy, Separator, Clear, Separator, Restore
                if (items.Count >= 1 && items[0] is System.Windows.Controls.MenuItem findItem)
                    findItem.Header = L("menu.find", "Find...  Ctrl+F");
                if (items.Count >= 3 && items[2] is System.Windows.Controls.MenuItem saveItem)
                    saveItem.Header = L("menu.saveChat", "Save chat as text...");
                if (items.Count >= 4 && items[3] is System.Windows.Controls.MenuItem copyItem)
                    copyItem.Header = L("menu.copyChat", "Copy chat as text");
                if (items.Count >= 6 && items[5] is System.Windows.Controls.MenuItem clearItem)
                    clearItem.Header = L("menu.clearConversation", "Clear conversation");
                if (items.Count >= 8 && items[7] is System.Windows.Controls.MenuItem restoreItem)
                    restoreItem.Header = L("menu.restoreWindowSizes", "Restore original window sizes");
            }
            UpdatePinButton();

            var warningText = LargeFileWarning.Child as TextBlock;
            if (warningText != null)
                warningText.Text = L("warning.largeFile", "⚠ Large file — provider may reject");

            Resources["PlaceholderSearchText"] = L("placeholder.search", "Search…");
            FlowDirection = LocalizationService.IsRightToLeftLanguage(LocalizationService.CurrentCode)
                ? System.Windows.FlowDirection.RightToLeft
                : System.Windows.FlowDirection.LeftToRight;

            // Re-resolve the current mode display name in the new language so the
            // mode button label and tooltip reflect the active language immediately.
            _assistantModeDisplayName = _assistantModeApplicationService.ResolveMode(_assistantModeKey).DisplayName;
            UpdateModeButtonState();
            UpdateVoiceOutputButton();
            SetMicButtonState(_speechService.IsListening ? MicState.Recording : MicState.Idle);
            RefreshToolsCategoryMenuLocalization();
            UpdateToolsButtonState();
        }
    }
}
