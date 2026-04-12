using System.Collections.Generic;
using System.Windows;
using Aire.Services;
using Aire.UI.MainWindow.Controls;

namespace Aire
{
    public partial class MainWindow
    {
        private List<CommandItem>? _commandPaletteItems;

        private void InitializeCommandPalette()
        {
            _commandPaletteItems = new List<CommandItem>
            {
                new CommandItem
                {
                    Name = LocalizationService.S("cmd.newConversation", "New Conversation"),
                    Shortcut = "Ctrl+N",
                    Execute = () => NewChatButton_Click(null, null)
                },
                new CommandItem
                {
                    Name = LocalizationService.S("cmd.toggleTools", "Toggle Tools Menu"),
                    Shortcut = "",
                    Execute = () => ToolsButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent))
                },
                new CommandItem
                {
                    Name = LocalizationService.S("cmd.toggleAgentMode", "Toggle Agent Mode"),
                    Shortcut = "",
                    Execute = () => AgentModeButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ToggleButton.ClickEvent))
                },
                new CommandItem
                {
                    Name = LocalizationService.S("cmd.openSettings", "Open Settings"),
                    Shortcut = "Ctrl+,",
                    Execute = () => _ = ShowSettingsWindowAsync()
                },
            };

            CommandPaletteControl.SetCommands(_commandPaletteItems);
            CommandPaletteControl.ClosedExternally += () => CommandPalettePopup.IsOpen = false;
        }

        private void ToggleCommandPalette()
        {
            if (CommandPalettePopup.IsOpen)
            {
                CommandPalettePopup.IsOpen = false;
                return;
            }

            InitializeCommandPalette();
            CommandPalettePopup.IsOpen = true;
            CommandPaletteControl.Open();
        }
    }
}
