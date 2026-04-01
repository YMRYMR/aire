using Aire.UI.Settings.Controls;
using Border = System.Windows.Controls.Border;
using Button = System.Windows.Controls.Button;
using ComboBox = System.Windows.Controls.ComboBox;
using ListView = System.Windows.Controls.ListView;
using PasswordBox = System.Windows.Controls.PasswordBox;
using StackPanel = System.Windows.Controls.StackPanel;
using TextBox = System.Windows.Controls.TextBox;
using TextBlock = System.Windows.Controls.TextBlock;

namespace Aire.UI
{
    public partial class SettingsWindow
    {
        private EmailConnectionsPaneControl EmailConnectionsPane => EmailConnectionsPaneControl;
        private TextBlock EmailAccountsTitle => EmailConnectionsPane.EmailAccountsTitle;
        private TextBlock EmailAccountsDescription => EmailConnectionsPane.EmailAccountsDescription;
        private Button AddGmailBtn => EmailConnectionsPane.AddGmailBtn;
        private Button AddOutlookBtn => EmailConnectionsPane.AddOutlookBtn;
        private Button AddCustomEmailBtn => EmailConnectionsPane.AddCustomEmailBtn;
        private ListView EmailAccountsList => EmailConnectionsPane.EmailAccountsList;
        private Border EmailEditPanel => EmailConnectionsPane.EmailEditPanel;
        private TextBlock EmailEditTitle => EmailConnectionsPane.EmailEditTitle;
        private TextBox EmailDisplayNameBox => EmailConnectionsPane.EmailDisplayNameBox;
        private ComboBox EmailProviderCombo => EmailConnectionsPane.EmailProviderCombo;
        private StackPanel EmailHostsPanel => EmailConnectionsPane.EmailHostsPanel;
        private TextBox EmailImapHostBox => EmailConnectionsPane.EmailImapHostBox;
        private TextBox EmailImapPortBox => EmailConnectionsPane.EmailImapPortBox;
        private TextBox EmailSmtpHostBox => EmailConnectionsPane.EmailSmtpHostBox;
        private TextBox EmailSmtpPortBox => EmailConnectionsPane.EmailSmtpPortBox;
        private TextBox EmailUsernameBox => EmailConnectionsPane.EmailUsernameBox;
        private StackPanel EmailPasswordPanel => EmailConnectionsPane.EmailPasswordPanel;
        private PasswordBox EmailPasswordBox => EmailConnectionsPane.EmailPasswordBox;
        private StackPanel EmailOAuthPanel => EmailConnectionsPane.EmailOAuthPanel;
        private Button SignInWithGoogleBtn => EmailConnectionsPane.SignInWithGoogleBtn;
        private TextBlock OAuthStatusText => EmailConnectionsPane.OAuthStatusText;
        private Button SaveEmailBtn => EmailConnectionsPane.SaveEmailBtn;
        private Button CancelEmailBtn => EmailConnectionsPane.CancelEmailBtn;
        private Button TestEmailConnBtn => EmailConnectionsPane.TestEmailConnBtn;
        private TextBlock EmailTestResult => EmailConnectionsPane.EmailTestResult;
    }
}
