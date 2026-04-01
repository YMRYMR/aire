using RoutedPropertyChangedEventArgs = System.Windows.RoutedPropertyChangedEventArgs<double>;
using RoutedPropertyChangedEventHandler = System.Windows.RoutedPropertyChangedEventHandler<double>;
using SelectionChangedEventArgs = System.Windows.Controls.SelectionChangedEventArgs;
using SelectionChangedEventHandler = System.Windows.Controls.SelectionChangedEventHandler;
using ComboBox = System.Windows.Controls.ComboBox;
using Slider = System.Windows.Controls.Slider;
using StackPanel = System.Windows.Controls.StackPanel;
using TextBlock = System.Windows.Controls.TextBlock;
using UserControl = System.Windows.Controls.UserControl;

namespace Aire.UI.Settings.Controls
{
    public partial class AppearanceSettingsControl : UserControl
    {
        public AppearanceSettingsControl()
        {
            InitializeComponent();
        }

        public StackPanel AppearanceSection => PART_AppearanceSection;
        public TextBlock BrightnessLabel => PART_BrightnessLabel;
        public TextBlock BrightnessValueLabel => PART_BrightnessValueLabel;
        public Slider BrightnessSlider => PART_BrightnessSlider;
        public TextBlock ColorTintLabel => PART_ColorTintLabel;
        public TextBlock TintValueLabel => PART_TintValueLabel;
        public Slider TintSlider => PART_TintSlider;
        public TextBlock NeutralLeftLabel => PART_NeutralLeftLabel;
        public TextBlock NeutralRightLabel => PART_NeutralRightLabel;
        public TextBlock AccentBrightnessLabel => PART_AccentBrightnessLabel;
        public Slider AccentBrightnessSlider => PART_AccentBrightnessSlider;
        public TextBlock AccentTintLabel => PART_AccentTintLabel;
        public Slider AccentTintSlider => PART_AccentTintSlider;
        public TextBlock AccentNeutralLeftLabel => PART_AccentNeutralLeftLabel;
        public TextBlock AccentNeutralRightLabel => PART_AccentNeutralRightLabel;
        public TextBlock FontSizeLabel => PART_FontSizeLabel;
        public TextBlock FontSizeDisplay => PART_FontSizeDisplay;
        public Slider FontSizeSlider => PART_FontSizeSlider;
        public TextBlock LanguageLabel => PART_LanguageLabel;
        public ComboBox LanguageComboBox => PART_LanguageComboBox;

        public event RoutedPropertyChangedEventHandler? BrightnessSliderValueChanged;
        public event RoutedPropertyChangedEventHandler? TintSliderValueChanged;
        public event RoutedPropertyChangedEventHandler? AccentBrightnessSliderValueChanged;
        public event RoutedPropertyChangedEventHandler? AccentTintSliderValueChanged;
        public event RoutedPropertyChangedEventHandler? FontSizeSliderValueChanged;
        public event SelectionChangedEventHandler? LanguageSelectionChanged;

        private void BrightnessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs e) => BrightnessSliderValueChanged?.Invoke(sender, e);
        private void TintSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs e) => TintSliderValueChanged?.Invoke(sender, e);
        private void AccentBrightnessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs e) => AccentBrightnessSliderValueChanged?.Invoke(sender, e);
        private void AccentTintSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs e) => AccentTintSliderValueChanged?.Invoke(sender, e);
        private void FontSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs e) => FontSizeSliderValueChanged?.Invoke(sender, e);
        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => LanguageSelectionChanged?.Invoke(sender, e);
    }
}
