using Aire.UI.Settings.Controls;
using Border = System.Windows.Controls.Border;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using ComboBox = System.Windows.Controls.ComboBox;
using StackPanel = System.Windows.Controls.StackPanel;
using TextBlock = System.Windows.Controls.TextBlock;

namespace Aire.UI
{
    public partial class SettingsWindow
    {
        private VoiceSettingsPaneControl VoiceSettingsPane => VoiceSettingsPaneControl;
        private StackPanel VoiceSection => VoiceSettingsPane.VoiceSection;
        private TextBlock VoiceVoiceLabel => VoiceSettingsPane.VoiceVoiceLabel;
        private ComboBox VoiceComboBox => VoiceSettingsPane.VoiceComboBox;
        private Button TestVoiceButton => VoiceSettingsPane.TestVoiceButton;
        private Border VoiceTestErrorBorder => VoiceSettingsPane.VoiceTestErrorBorder;
        private TextBlock VoiceTestErrorText => VoiceSettingsPane.VoiceTestErrorText;
        private CheckBox VoiceLocalOnlyCheckBox => VoiceSettingsPane.VoiceLocalOnlyCheckBox;
        private Button DownloadVoicesButton => VoiceSettingsPane.DownloadVoicesButton;
        private TextBlock VoiceSpeedLabel => VoiceSettingsPane.VoiceSpeedLabel;
        private TextBlock VoiceSpeedDisplay => VoiceSettingsPane.VoiceSpeedDisplay;
    }
}
