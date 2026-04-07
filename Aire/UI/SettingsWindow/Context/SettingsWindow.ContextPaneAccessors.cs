using Aire.UI.Settings.Controls;
using Button = System.Windows.Controls.Button;
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
        private Button RestoreDefaultsButton => ContextPane.RestoreDefaultsButton;
        private TextBlock ContextHistoryHeader => ContextPane.ContextHistoryHeader;
        private TextBlock MaxMessagesSubLabel => ContextPane.MaxMessagesSubLabel;
        private TextBlock AnchorMessagesSubLabel => ContextPane.AnchorMessagesSubLabel;
        private TextBlock ContextCachingHeader => ContextPane.ContextCachingHeader;
        private TextBlock PromptCachingLabel => ContextPane.PromptCachingLabel;
        private TextBlock PromptCachingSubLabel => ContextPane.PromptCachingSubLabel;
        private TextBlock UncachedRecentMessagesSubLabel => ContextPane.UncachedRecentMessagesSubLabel;
        private TextBlock ContextSummariesHeader => ContextPane.ContextSummariesHeader;
        private TextBlock AutoSummariseLabel => ContextPane.AutoSummariseLabel;
        private TextBlock AutoSummariseSubLabel => ContextPane.AutoSummariseSubLabel;
        private TextBlock SummaryMaxCharactersSubLabel => ContextPane.SummaryMaxCharactersSubLabel;
    }
}
