using Aire.UI.Settings.Controls;
using ComboBox = System.Windows.Controls.ComboBox;
using Slider = System.Windows.Controls.Slider;
using StackPanel = System.Windows.Controls.StackPanel;
using TextBlock = System.Windows.Controls.TextBlock;

namespace Aire.UI
{
    public partial class SettingsWindow
    {
        private AppearanceSettingsControl AppearanceSettingsPane => AppearanceSettingsControl;
        private StackPanel AppearanceSection => AppearanceSettingsPane.AppearanceSection;
        private TextBlock BrightnessLabel => AppearanceSettingsPane.BrightnessLabel;
        private TextBlock BrightnessValueLabel => AppearanceSettingsPane.BrightnessValueLabel;
        private Slider BrightnessSlider => AppearanceSettingsPane.BrightnessSlider;
        private TextBlock ColorTintLabel => AppearanceSettingsPane.ColorTintLabel;
        private TextBlock TintValueLabel => AppearanceSettingsPane.TintValueLabel;
        private Slider TintSlider => AppearanceSettingsPane.TintSlider;
        private TextBlock NeutralLeftLabel => AppearanceSettingsPane.NeutralLeftLabel;
        private TextBlock NeutralRightLabel => AppearanceSettingsPane.NeutralRightLabel;
        private TextBlock AccentBrightnessLabel => AppearanceSettingsPane.AccentBrightnessLabel;
        private Slider AccentBrightnessSlider => AppearanceSettingsPane.AccentBrightnessSlider;
        private TextBlock AccentTintLabel => AppearanceSettingsPane.AccentTintLabel;
        private Slider AccentTintSlider => AppearanceSettingsPane.AccentTintSlider;
        private TextBlock AccentNeutralLeftLabel => AppearanceSettingsPane.AccentNeutralLeftLabel;
        private TextBlock AccentNeutralRightLabel => AppearanceSettingsPane.AccentNeutralRightLabel;
        private TextBlock FontSizeLabel => AppearanceSettingsPane.FontSizeLabel;
        private TextBlock FontSizeDisplay => AppearanceSettingsPane.FontSizeDisplay;
        private Slider FontSizeSlider => AppearanceSettingsPane.FontSizeSlider;
        private TextBlock LanguageLabel => AppearanceSettingsPane.LanguageLabel;
        private ComboBox LanguageComboBox => AppearanceSettingsPane.LanguageComboBox;
    }
}
