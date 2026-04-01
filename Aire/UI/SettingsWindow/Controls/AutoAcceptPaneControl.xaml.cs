using RoutedEventArgs = System.Windows.RoutedEventArgs;
using RoutedEventHandler = System.Windows.RoutedEventHandler;
using CheckBox = System.Windows.Controls.CheckBox;
using Grid = System.Windows.Controls.Grid;
using StackPanel = System.Windows.Controls.StackPanel;
using UserControl = System.Windows.Controls.UserControl;

namespace Aire.UI.Settings.Controls
{
    public partial class AutoAcceptPaneControl : UserControl
    {
        public AutoAcceptPaneControl()
        {
            InitializeComponent();
        }

        public StackPanel AutoAcceptSection => PART_AutoAcceptSection;
        public CheckBox AutoAcceptEnabledCheckBox => PART_AutoAcceptEnabledCheckBox;
        public Grid AutoAcceptToolsPanel => PART_AutoAcceptToolsPanel;
        public CheckBox AutoAcceptOpenUrlCheckBox => PART_AutoAcceptOpenUrlCheckBox;
        public CheckBox AutoAcceptHttpRequestCheckBox => PART_AutoAcceptHttpRequestCheckBox;
        public CheckBox AutoAcceptOpenBrowserTabCheckBox => PART_AutoAcceptOpenBrowserTabCheckBox;
        public CheckBox AutoAcceptListBrowserTabsCheckBox => PART_AutoAcceptListBrowserTabsCheckBox;
        public CheckBox AutoAcceptReadBrowserTabCheckBox => PART_AutoAcceptReadBrowserTabCheckBox;
        public CheckBox AutoAcceptSwitchBrowserTabCheckBox => PART_AutoAcceptSwitchBrowserTabCheckBox;
        public CheckBox AutoAcceptCloseBrowserTabCheckBox => PART_AutoAcceptCloseBrowserTabCheckBox;
        public CheckBox AutoAcceptGetBrowserHtmlCheckBox => PART_AutoAcceptGetBrowserHtmlCheckBox;
        public CheckBox AutoAcceptExecuteBrowserScriptCheckBox => PART_AutoAcceptExecuteBrowserScriptCheckBox;
        public CheckBox AutoAcceptGetBrowserCookiesCheckBox => PART_AutoAcceptGetBrowserCookiesCheckBox;
        public CheckBox AutoAcceptExecuteCommandCheckBox => PART_AutoAcceptExecuteCommandCheckBox;
        public CheckBox AutoAcceptReadCommandOutputCheckBox => PART_AutoAcceptReadCommandOutputCheckBox;
        public CheckBox AutoAcceptListFilesCheckBox => PART_AutoAcceptListFilesCheckBox;
        public CheckBox AutoAcceptReadFileCheckBox => PART_AutoAcceptReadFileCheckBox;
        public CheckBox AutoAcceptSearchFilesCheckBox => PART_AutoAcceptSearchFilesCheckBox;
        public CheckBox AutoAcceptSearchFileContentCheckBox => PART_AutoAcceptSearchFileContentCheckBox;
        public CheckBox AutoAcceptWriteToFileCheckBox => PART_AutoAcceptWriteToFileCheckBox;
        public CheckBox AutoAcceptApplyDiffCheckBox => PART_AutoAcceptApplyDiffCheckBox;
        public CheckBox AutoAcceptCreateDirectoryCheckBox => PART_AutoAcceptCreateDirectoryCheckBox;
        public CheckBox AutoAcceptDeleteFileCheckBox => PART_AutoAcceptDeleteFileCheckBox;
        public CheckBox AutoAcceptMoveFileCheckBox => PART_AutoAcceptMoveFileCheckBox;
        public CheckBox AutoAcceptOpenFileCheckBox => PART_AutoAcceptOpenFileCheckBox;
        public CheckBox AutoAcceptGetClipboardCheckBox => PART_AutoAcceptGetClipboardCheckBox;
        public CheckBox AutoAcceptSetClipboardCheckBox => PART_AutoAcceptSetClipboardCheckBox;
        public CheckBox AutoAcceptNotifyCheckBox => PART_AutoAcceptNotifyCheckBox;
        public CheckBox AutoAcceptGetSystemInfoCheckBox => PART_AutoAcceptGetSystemInfoCheckBox;
        public CheckBox AutoAcceptGetRunningProcessesCheckBox => PART_AutoAcceptGetRunningProcessesCheckBox;
        public CheckBox AutoAcceptGetActiveWindowCheckBox => PART_AutoAcceptGetActiveWindowCheckBox;
        public CheckBox AutoAcceptGetSelectedTextCheckBox => PART_AutoAcceptGetSelectedTextCheckBox;
        public CheckBox AutoAcceptRememberCheckBox => PART_AutoAcceptRememberCheckBox;
        public CheckBox AutoAcceptRecallCheckBox => PART_AutoAcceptRecallCheckBox;
        public CheckBox AutoAcceptSetReminderCheckBox => PART_AutoAcceptSetReminderCheckBox;
        public CheckBox AutoAcceptReadEmailsCheckBox => PART_AutoAcceptReadEmailsCheckBox;
        public CheckBox AutoAcceptSearchEmailsCheckBox => PART_AutoAcceptSearchEmailsCheckBox;
        public CheckBox AutoAcceptSendEmailCheckBox => PART_AutoAcceptSendEmailCheckBox;
        public CheckBox AutoAcceptReplyToEmailCheckBox => PART_AutoAcceptReplyToEmailCheckBox;
        public CheckBox AutoAcceptNewTaskCheckBox => PART_AutoAcceptNewTaskCheckBox;
        public CheckBox AutoAcceptAskFollowupQuestionCheckBox => PART_AutoAcceptAskFollowupQuestionCheckBox;
        public CheckBox AutoAcceptAttemptCompletionCheckBox => PART_AutoAcceptAttemptCompletionCheckBox;
        public CheckBox AutoAcceptSkillCheckBox => PART_AutoAcceptSkillCheckBox;
        public CheckBox AutoAcceptSwitchModeCheckBox => PART_AutoAcceptSwitchModeCheckBox;
        public CheckBox AutoAcceptSwitchModelCheckBox => PART_AutoAcceptSwitchModelCheckBox;
        public CheckBox AutoAcceptUpdateTodoListCheckBox => PART_AutoAcceptUpdateTodoListCheckBox;
        public CheckBox AutoAcceptShowImageCheckBox => PART_AutoAcceptShowImageCheckBox;
        public CheckBox AutoAcceptMouseToolsCheckBox => PART_AutoAcceptMouseToolsCheckBox;
        public CheckBox AutoAcceptKeyboardToolsCheckBox => PART_AutoAcceptKeyboardToolsCheckBox;

        public event RoutedEventHandler? AutoAcceptEnabledChanged;

        private void AutoAcceptEnabledCheckBox_Changed(object sender, RoutedEventArgs e) => AutoAcceptEnabledChanged?.Invoke(sender, e);
    }
}
