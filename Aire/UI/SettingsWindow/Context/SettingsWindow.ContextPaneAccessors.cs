using Aire.UI.Settings.Controls;
using CheckBox = System.Windows.Controls.CheckBox;
using TextBlock = System.Windows.Controls.TextBlock;
using TextBox = System.Windows.Controls.TextBox;

namespace Aire.UI
{
    public partial class SettingsWindow
    {
        private ContextSettingsPaneControl ContextPane => ContextSettingsPaneControl;
        private TextBlock ContextDescriptionText => ContextPane.ContextDescriptionText;
        private CheckBox EnablePromptCachingCheckBox => ContextPane.EnablePromptCachingCheckBox;
        private CheckBox EnableConversationSummariesCheckBox => ContextPane.EnableConversationSummariesCheckBox;
        private TextBlock MaxMessagesLabel => ContextPane.MaxMessagesLabel;
        private TextBox MaxMessagesTextBox => ContextPane.MaxMessagesTextBox;
        private TextBlock AnchorMessagesLabel => ContextPane.AnchorMessagesLabel;
        private TextBox AnchorMessagesTextBox => ContextPane.AnchorMessagesTextBox;
        private TextBlock UncachedRecentMessagesLabel => ContextPane.UncachedRecentMessagesLabel;
        private TextBox UncachedRecentMessagesTextBox => ContextPane.UncachedRecentMessagesTextBox;
        private TextBlock SummaryMaxCharactersLabel => ContextPane.SummaryMaxCharactersLabel;
        private TextBox SummaryMaxCharactersTextBox => ContextPane.SummaryMaxCharactersTextBox;
        private TextBlock ContextHintText => ContextPane.ContextHintText;
    }
}
