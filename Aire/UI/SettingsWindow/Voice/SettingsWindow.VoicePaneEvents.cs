namespace Aire.UI
{
    public partial class SettingsWindow
    {
        private void WireVoicePaneEvents()
        {
            VoiceSettingsPane.VoiceComboSelectionChanged += VoiceComboBox_SelectionChanged;
            VoiceSettingsPane.TestVoiceClicked += TestVoiceButton_Click;
            VoiceSettingsPane.VoiceLocalOnlyChanged += VoiceLocalOnly_Changed;
            VoiceSettingsPane.DownloadVoicesClicked += DownloadVoicesButton_Click;
            VoiceSettingsPane.VoiceSpeedDecreaseClicked += VoiceSpeedDecrease_Click;
            VoiceSettingsPane.VoiceSpeedIncreaseClicked += VoiceSpeedIncrease_Click;
        }
    }
}
