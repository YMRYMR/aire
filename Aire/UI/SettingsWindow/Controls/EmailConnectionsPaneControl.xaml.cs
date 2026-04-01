using RoutedEventArgs = System.Windows.RoutedEventArgs;
using RoutedEventHandler = System.Windows.RoutedEventHandler;
using SelectionChangedEventArgs = System.Windows.Controls.SelectionChangedEventArgs;
using SelectionChangedEventHandler = System.Windows.Controls.SelectionChangedEventHandler;
using Button = System.Windows.Controls.Button;
using ComboBox = System.Windows.Controls.ComboBox;
using ComboBoxItem = System.Windows.Controls.ComboBoxItem;
using ListView = System.Windows.Controls.ListView;
using PasswordBox = System.Windows.Controls.PasswordBox;
using StackPanel = System.Windows.Controls.StackPanel;
using TextBox = System.Windows.Controls.TextBox;
using TextBlock = System.Windows.Controls.TextBlock;
using Border = System.Windows.Controls.Border;
using UserControl = System.Windows.Controls.UserControl;

namespace Aire.UI.Settings.Controls
{
    public partial class EmailConnectionsPaneControl : UserControl
    {
        public EmailConnectionsPaneControl()
        {
            InitializeComponent();
        }

        public TextBlock EmailAccountsTitle => PART_EmailAccountsTitle;
        public TextBlock EmailAccountsDescription => PART_EmailAccountsDescription;
        public Button AddGmailBtn => PART_AddGmailBtn;
        public Button AddOutlookBtn => PART_AddOutlookBtn;
        public Button AddCustomEmailBtn => PART_AddCustomEmailBtn;
        public ListView EmailAccountsList => PART_EmailAccountsList;
        public Border EmailEditPanel => PART_EmailEditPanel;
        public TextBlock EmailEditTitle => PART_EmailEditTitle;
        public TextBox EmailDisplayNameBox => PART_EmailDisplayNameBox;
        public ComboBox EmailProviderCombo => PART_EmailProviderCombo;
        public StackPanel EmailHostsPanel => PART_EmailHostsPanel;
        public TextBox EmailImapHostBox => PART_EmailImapHostBox;
        public TextBox EmailImapPortBox => PART_EmailImapPortBox;
        public TextBox EmailSmtpHostBox => PART_EmailSmtpHostBox;
        public TextBox EmailSmtpPortBox => PART_EmailSmtpPortBox;
        public TextBox EmailUsernameBox => PART_EmailUsernameBox;
        public StackPanel EmailPasswordPanel => PART_EmailPasswordPanel;
        public PasswordBox EmailPasswordBox => PART_EmailPasswordBox;
        public StackPanel EmailOAuthPanel => PART_EmailOAuthPanel;
        public Button SignInWithGoogleBtn => PART_SignInWithGoogleBtn;
        public TextBlock OAuthStatusText => PART_OAuthStatusText;
        public Button SaveEmailBtn => PART_SaveEmailBtn;
        public Button CancelEmailBtn => PART_CancelEmailBtn;
        public Button TestEmailConnBtn => PART_TestEmailConnBtn;
        public TextBlock EmailTestResult => PART_EmailTestResult;

        public event RoutedEventHandler? AddGmailClicked;
        public event RoutedEventHandler? AddOutlookClicked;
        public event RoutedEventHandler? AddCustomEmailClicked;
        public event SelectionChangedEventHandler? EmailAccountsSelectionChanged;
        public event RoutedEventHandler? TestEmailClicked;
        public event RoutedEventHandler? DeleteEmailClicked;
        public event SelectionChangedEventHandler? EmailProviderSelectionChanged;
        public event RoutedEventHandler? SignInWithGoogleClicked;
        public event RoutedEventHandler? SaveEmailClicked;
        public event RoutedEventHandler? CancelEmailClicked;
        public event RoutedEventHandler? TestEmailConnectionClicked;

        private void AddGmailBtn_Click(object sender, RoutedEventArgs e) => AddGmailClicked?.Invoke(sender, e);
        private void AddOutlookBtn_Click(object sender, RoutedEventArgs e) => AddOutlookClicked?.Invoke(sender, e);
        private void AddCustomEmailBtn_Click(object sender, RoutedEventArgs e) => AddCustomEmailClicked?.Invoke(sender, e);
        private void EmailAccountsList_SelectionChanged(object sender, SelectionChangedEventArgs e) => EmailAccountsSelectionChanged?.Invoke(sender, e);
        private void TestEmailBtn_Click(object sender, RoutedEventArgs e) => TestEmailClicked?.Invoke(sender, e);
        private void DeleteEmailBtn_Click(object sender, RoutedEventArgs e) => DeleteEmailClicked?.Invoke(sender, e);
        private void EmailProviderCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => EmailProviderSelectionChanged?.Invoke(sender, e);
        private void SignInWithGoogleBtn_Click(object sender, RoutedEventArgs e) => SignInWithGoogleClicked?.Invoke(sender, e);
        private void SaveEmailBtn_Click(object sender, RoutedEventArgs e) => SaveEmailClicked?.Invoke(sender, e);
        private void CancelEmailBtn_Click(object sender, RoutedEventArgs e) => CancelEmailClicked?.Invoke(sender, e);
        private void TestEmailConnBtn_Click(object sender, RoutedEventArgs e) => TestEmailConnectionClicked?.Invoke(sender, e);
    }
}
