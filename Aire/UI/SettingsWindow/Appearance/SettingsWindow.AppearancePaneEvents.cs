namespace Aire.UI
{
    public partial class SettingsWindow
    {
        private void WireAppearancePaneEvents()
        {
            AppearanceSettingsPane.BrightnessSliderValueChanged += BrightnessSlider_ValueChanged;
            AppearanceSettingsPane.TintSliderValueChanged += TintSlider_ValueChanged;
            AppearanceSettingsPane.AccentBrightnessSliderValueChanged += AccentBrightnessSlider_ValueChanged;
            AppearanceSettingsPane.AccentTintSliderValueChanged += AccentTintSlider_ValueChanged;
            AppearanceSettingsPane.FontSizeSliderValueChanged += FontSizeSlider_ValueChanged;
            AppearanceSettingsPane.LanguageSelectionChanged += LanguageComboBox_SelectionChanged;
        }
    }
}
