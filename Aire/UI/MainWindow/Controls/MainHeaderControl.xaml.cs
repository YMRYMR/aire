using System;
using System.Windows;

namespace Aire.UI.MainWindow.Controls
{
    public partial class MainHeaderControl : System.Windows.Controls.UserControl
    {
        public MainHeaderControl()
        {
            InitializeComponent();
        }

        internal System.Windows.Controls.Button? _testSidebarToggleButton;
        internal System.Windows.Controls.Button? _testHelpButton;
        internal System.Windows.Controls.Button? _testSettingsButton;
        internal System.Windows.Controls.ComboBox? _testProviderComboBox;
        internal System.Windows.Controls.Button? _testCheckAgainButton;
        internal System.Windows.Controls.Button? _testPinButton;
        internal System.Windows.Controls.Button? _testVoiceOutputButton;
        internal System.Windows.Controls.Button? _testBrowserButton;

        public System.Windows.Controls.Button SidebarToggleButton => _testSidebarToggleButton ?? PART_SidebarToggleButton;
        public System.Windows.Controls.Button HelpButton => _testHelpButton ?? PART_HelpButton;
        public System.Windows.Controls.Button SettingsButton => _testSettingsButton ?? PART_SettingsButton;
        public System.Windows.Controls.ComboBox ProviderComboBox => _testProviderComboBox ?? PART_ProviderComboBox;
        public System.Windows.Controls.Button CheckAgainButton => _testCheckAgainButton ?? PART_CheckAgainButton;
        public System.Windows.Controls.Button PinButton => _testPinButton ?? PART_PinButton;
        public System.Windows.Controls.Button VoiceOutputButton => _testVoiceOutputButton ?? PART_VoiceOutputButton;
        public System.Windows.Controls.Button BrowserButton => _testBrowserButton ?? PART_BrowserButton;

        public event System.Windows.Input.MouseButtonEventHandler? HeaderMouseLeftButtonDown;
        public event RoutedEventHandler? SidebarToggleClicked;
        public event RoutedEventHandler? HelpClicked;
        public event RoutedEventHandler? SettingsClicked;
        public event System.Windows.Controls.SelectionChangedEventHandler? ProviderSelectionChanged;
        public event EventHandler? ProviderDropDownOpened;
        public event RoutedEventHandler? CheckAgainClicked;
        public event RoutedEventHandler? PinClicked;
        public event RoutedEventHandler? VoiceOutputClicked;
        public event RoutedEventHandler? BrowserClicked;

        private void HeaderBorder_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
            => HeaderMouseLeftButtonDown?.Invoke(sender, e);

        private void SidebarToggleButton_Click(object sender, RoutedEventArgs e)
            => SidebarToggleClicked?.Invoke(sender, e);

        private void HelpButton_Click(object sender, RoutedEventArgs e)
            => HelpClicked?.Invoke(sender, e);

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
            => SettingsClicked?.Invoke(sender, e);

        private void ProviderComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
            => ProviderSelectionChanged?.Invoke(sender, e);

        private void ProviderComboBox_DropDownOpened(object sender, EventArgs e)
            => ProviderDropDownOpened?.Invoke(sender, e);

        private void CheckAgainButton_Click(object sender, RoutedEventArgs e)
            => CheckAgainClicked?.Invoke(sender, e);

        private void PinButton_Click(object sender, RoutedEventArgs e)
            => PinClicked?.Invoke(sender, e);

        private void VoiceOutputButton_Click(object sender, RoutedEventArgs e)
            => VoiceOutputClicked?.Invoke(sender, e);

        private void BrowserButton_Click(object sender, RoutedEventArgs e)
            => BrowserClicked?.Invoke(sender, e);
    }
}
