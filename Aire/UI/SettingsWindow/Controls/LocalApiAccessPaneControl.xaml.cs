using RoutedEventArgs = System.Windows.RoutedEventArgs;
using RoutedEventHandler = System.Windows.RoutedEventHandler;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using TextBox = System.Windows.Controls.TextBox;
using TextBlock = System.Windows.Controls.TextBlock;
using UserControl = System.Windows.Controls.UserControl;

namespace Aire.UI.Settings.Controls
{
    public partial class LocalApiAccessPaneControl : UserControl
    {
        public LocalApiAccessPaneControl()
        {
            InitializeComponent();
        }

        public TextBlock ApiAccessTitle => PART_ApiAccessTitle;
        public TextBlock ApiAccessDescription => PART_ApiAccessDescription;
        public CheckBox ApiAccessEnabledCheckBox => PART_ApiAccessEnabledCheckBox;
        public TextBlock ApiAccessTokenTitle => PART_ApiAccessTokenTitle;
        public TextBlock ApiAccessTokenDescription => PART_ApiAccessTokenDescription;
        public TextBox ApiAccessTokenBox => PART_ApiAccessTokenBox;
        public TextBox ApiPortBox => PART_ApiPortBox;
        public Button CopyApiAccessTokenButton => PART_CopyApiAccessTokenButton;
        public Button RegenerateApiAccessTokenButton => PART_RegenerateApiAccessTokenButton;

        public event RoutedEventHandler? ApiAccessEnabledChanged;
        public event RoutedEventHandler? ApiPortChanged;
        public event RoutedEventHandler? CopyApiAccessTokenClicked;
        public event RoutedEventHandler? RegenerateApiAccessTokenClicked;

        private void ApiAccessEnabledCheckBox_Changed(object sender, RoutedEventArgs e) => ApiAccessEnabledChanged?.Invoke(sender, e);
        private void ApiPortBox_Changed(object sender, RoutedEventArgs e) => ApiPortChanged?.Invoke(sender, e);
        private void CopyApiAccessTokenButton_Click(object sender, RoutedEventArgs e) => CopyApiAccessTokenClicked?.Invoke(sender, e);
        private void RegenerateApiAccessTokenButton_Click(object sender, RoutedEventArgs e) => RegenerateApiAccessTokenClicked?.Invoke(sender, e);
    }
}
