using RoutedEventArgs = System.Windows.RoutedEventArgs;
using RoutedEventHandler = System.Windows.RoutedEventHandler;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using StackPanel = System.Windows.Controls.StackPanel;
using TextBlock = System.Windows.Controls.TextBlock;
using TextBox = System.Windows.Controls.TextBox;
using UserControl = System.Windows.Controls.UserControl;

namespace Aire.UI.Settings.Controls
{
    public partial class ContextSettingsPaneControl : UserControl
    {
        public ContextSettingsPaneControl()
        {
            InitializeComponent();
        }

        public StackPanel ContextSection => PART_ContextSection;
        public TextBlock ContextDescriptionText => PART_ContextDescriptionText;
        public CheckBox EnablePromptCachingCheckBox => PART_EnablePromptCachingCheckBox;
        public CheckBox EnableConversationSummariesCheckBox => PART_EnableConversationSummariesCheckBox;
        public TextBlock MaxMessagesLabel => PART_MaxMessagesLabel;
        public TextBox MaxMessagesTextBox => PART_MaxMessagesTextBox;
        public TextBlock AnchorMessagesLabel => PART_AnchorMessagesLabel;
        public TextBox AnchorMessagesTextBox => PART_AnchorMessagesTextBox;
        public TextBlock UncachedRecentMessagesLabel => PART_UncachedRecentMessagesLabel;
        public TextBox UncachedRecentMessagesTextBox => PART_UncachedRecentMessagesTextBox;
        public TextBlock SummaryMaxCharactersLabel => PART_SummaryMaxCharactersLabel;
        public TextBox SummaryMaxCharactersTextBox => PART_SummaryMaxCharactersTextBox;
        public TextBlock ContextHintText => PART_ContextHintText;
        public Button RestoreDefaultsButton => PART_RestoreDefaultsButton;
        public TextBlock ContextHistoryHeader => PART_ContextHistoryHeader;
        public TextBlock MaxMessagesSubLabel => PART_MaxMessagesSubLabel;
        public TextBlock AnchorMessagesSubLabel => PART_AnchorMessagesSubLabel;
        public TextBlock ContextCachingHeader => PART_ContextCachingHeader;
        public TextBlock PromptCachingLabel => PART_PromptCachingLabel;
        public TextBlock PromptCachingSubLabel => PART_PromptCachingSubLabel;
        public TextBlock UncachedRecentMessagesSubLabel => PART_UncachedRecentMessagesSubLabel;
        public TextBlock ContextSummariesHeader => PART_ContextSummariesHeader;
        public TextBlock AutoSummariseLabel => PART_AutoSummariseLabel;
        public TextBlock AutoSummariseSubLabel => PART_AutoSummariseSubLabel;
        public TextBlock SummaryMaxCharactersSubLabel => PART_SummaryMaxCharactersSubLabel;

        public event RoutedEventHandler? ContextSettingChangedRequested;
        public event RoutedEventHandler? RestoreDefaultsRequested;

        private void ContextSettingChanged(object sender, RoutedEventArgs e)
            => ContextSettingChangedRequested?.Invoke(sender, e);

        private void RestoreDefaultsButton_Click(object sender, RoutedEventArgs e)
            => RestoreDefaultsRequested?.Invoke(sender, e);
    }
}
