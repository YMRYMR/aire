using RoutedEventArgs = System.Windows.RoutedEventArgs;
using RoutedEventHandler = System.Windows.RoutedEventHandler;
using SelectionChangedEventArgs = System.Windows.Controls.SelectionChangedEventArgs;
using SelectionChangedEventHandler = System.Windows.Controls.SelectionChangedEventHandler;
using Border = System.Windows.Controls.Border;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using ComboBox = System.Windows.Controls.ComboBox;
using StackPanel = System.Windows.Controls.StackPanel;
using TextBlock = System.Windows.Controls.TextBlock;
using UserControl = System.Windows.Controls.UserControl;

namespace Aire.UI.Settings.Controls
{
    public partial class VoiceSettingsPaneControl : UserControl
    {
        public VoiceSettingsPaneControl()
        {
            InitializeComponent();
        }

        public StackPanel VoiceSection => PART_VoiceSection;
        public TextBlock VoiceVoiceLabel => PART_VoiceVoiceLabel;
        public ComboBox VoiceComboBox => PART_VoiceComboBox;
        public Button TestVoiceButton => PART_TestVoiceButton;
        public Border VoiceTestErrorBorder => PART_VoiceTestErrorBorder;
        public TextBlock VoiceTestErrorText => PART_VoiceTestErrorText;
        public CheckBox VoiceLocalOnlyCheckBox => PART_VoiceLocalOnlyCheckBox;
        public Button DownloadVoicesButton => PART_DownloadVoicesButton;
        public TextBlock VoiceSpeedLabel => PART_VoiceSpeedLabel;
        public TextBlock VoiceSpeedDisplay => PART_VoiceSpeedDisplay;

        public event SelectionChangedEventHandler? VoiceComboSelectionChanged;
        public event RoutedEventHandler? TestVoiceClicked;
        public event RoutedEventHandler? VoiceLocalOnlyChanged;
        public event RoutedEventHandler? DownloadVoicesClicked;
        public event RoutedEventHandler? VoiceSpeedDecreaseClicked;
        public event RoutedEventHandler? VoiceSpeedIncreaseClicked;

        private void VoiceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => VoiceComboSelectionChanged?.Invoke(sender, e);
        private void TestVoiceButton_Click(object sender, RoutedEventArgs e) => TestVoiceClicked?.Invoke(sender, e);
        private void VoiceLocalOnlyCheckBox_Changed(object sender, RoutedEventArgs e) => VoiceLocalOnlyChanged?.Invoke(sender, e);
        private void DownloadVoicesButton_Click(object sender, RoutedEventArgs e) => DownloadVoicesClicked?.Invoke(sender, e);
        private void VoiceSpeedDecreaseButton_Click(object sender, RoutedEventArgs e) => VoiceSpeedDecreaseClicked?.Invoke(sender, e);
        private void VoiceSpeedIncreaseButton_Click(object sender, RoutedEventArgs e) => VoiceSpeedIncreaseClicked?.Invoke(sender, e);
    }
}
