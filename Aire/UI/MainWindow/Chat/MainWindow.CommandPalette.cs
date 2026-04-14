using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Aire.Services;
using Aire.UI.MainWindow.Controls;

namespace Aire
{
    public partial class MainWindow
    {
        private List<CommandItem>? _commandPaletteItems;
        private PromptTemplateService? _promptTemplateService;

        private void InitializeCommandPalette()
        {
            _commandPaletteItems = new List<CommandItem>
            {
                new CommandItem
                {
                    Name = LocalizationService.S("cmd.newConversation", "New Conversation"),
                    Shortcut = "Ctrl+N",
                    Execute = () => NewChatButton_Click(this, new RoutedEventArgs())
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
                new CommandItem
                {
                    Name = LocalizationService.S("cmd.exportConversation", "Export Conversation"),
                    Shortcut = "",
                    Execute = () => ExportConversation_Click(this, new RoutedEventArgs())
                },
            };

            // Load prompt templates into command palette.
            _promptTemplateService = new PromptTemplateService();
            _promptTemplateService.Load();

            foreach (var template in _promptTemplateService.Templates)
            {
                var capturedTemplate = template;
                _commandPaletteItems.Add(new CommandItem
                {
                    Name = template.Name,
                    Shortcut = template.Shortcut ?? "",
                    Execute = () => ApplyPromptTemplate(capturedTemplate)
                });
            }

            CommandPaletteControl.SetCommands(_commandPaletteItems);
            CommandPaletteControl.ClosedExternally += () => CommandPalettePopup.IsOpen = false;
        }

        private void ApplyPromptTemplate(PromptTemplate template)
        {
            var text = template.Resolve();
            InputTextBox.Text = text;
            InputTextBox.CaretIndex = text.Length;
            InputTextBox.Focus();
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
