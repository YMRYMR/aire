using System;
using System.Windows;
using System.Windows.Controls;
using Aire.Services;

namespace Aire.UI
{
    public partial class SettingsWindow
    {
        private void BrightnessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_suppressAppearance)
            {
                return;
            }

            AppearanceService.Apply(BrightnessSlider.Value, AppearanceService.TintPosition);
            BrightnessValueLabel.Text = $"{e.NewValue:0.00}";
            AppearanceChanged?.Invoke();
        }

        private void OnThemeChanged()
        {
            Dispatcher.Invoke(() =>
            {
                FontSize = AppearanceService.FontSize;
                FontSizeDisplay.Text = $"{AppearanceService.FontSize:0}";
                _suppressAppearance = true;
                FontSizeSlider.Value = AppearanceService.FontSize;
                _suppressAppearance = false;
            });
        }

        private void TintSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_suppressAppearance)
            {
                return;
            }

            AppearanceService.Apply(AppearanceService.Brightness, e.NewValue);
            TintValueLabel.Text = $"{e.NewValue:0.000}";
            AppearanceChanged?.Invoke();
        }

        private void AccentBrightnessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_suppressAppearance)
            {
                return;
            }

            AppearanceService.ApplyAccent(e.NewValue, AppearanceService.AccentTintPosition);
            AppearanceChanged?.Invoke();
        }

        private void AccentTintSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_suppressAppearance)
            {
                return;
            }

            AppearanceService.ApplyAccent(AppearanceService.AccentBrightness, e.NewValue);
            AppearanceChanged?.Invoke();
        }

        private void FontSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_suppressAppearance)
            {
                return;
            }

            AppearanceService.SetFontSize(e.NewValue);
            FontSizeDisplay.Text = $"{e.NewValue:0}";
            AppearanceChanged?.Invoke();
        }

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressAppearance)
            {
                return;
            }

            var item = LanguageComboBox.SelectedItem as ComboBoxItem;
            if (item?.Tag is string code)
            {
                LocalizationService.SetLanguage(code);
            }
        }

        private void ApiAccessEnabled_Changed(object sender, RoutedEventArgs e)
        {
            if (_suppressApiAccess)
            {
                return;
            }

            AppState.SetApiAccessEnabled(ApiAccessEnabledCheckBox.IsChecked == true);
            ApiAccessTokenBox.Text = AppState.EnsureApiAccessToken();
        }

        private void ApiPort_Changed(object sender, RoutedEventArgs e)
        {
            if (_suppressApiAccess)
                return;

            var text = ApiPortBox.Text?.Trim() ?? string.Empty;
            if (int.TryParse(text, out var port) && port >= 1 && port <= 65535)
            {
                AppState.SetApiPort(port);
            }
            else
            {
                ApiPortBox.Text = AppState.GetApiPort().ToString();
            }
        }

        private void CopyApiAccessTokenButton_Click(object sender, RoutedEventArgs e)
        {
            var token = AppState.GetApiAccessToken().Trim();
            if (string.IsNullOrWhiteSpace(token))
            {
                return;
            }

            System.Windows.Clipboard.SetText(token);
            ShowToast(LocalizationService.S("toast.tokenCopied", "API token copied to clipboard."));
        }

        private void RegenerateApiAccessTokenButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ConfirmationDialog.ShowCentered(this,
                title: LocalizationService.S("settings.regenerateToken", "Regenerate API token?"),
                message: LocalizationService.S("settings.regenerateTokenMsg", "Existing local tools will stop working until they use the new token.")))
            {
                return;
            }

            ApiAccessTokenBox.Text = AppState.RegenerateApiAccessToken();
            ShowToast(LocalizationService.S("toast.tokenRegenerated", "API token regenerated."));
        }

        private void FontSizeDecrease_Click(object sender, RoutedEventArgs e)
        {
            if (_suppressAppearance)
            {
                return;
            }

            var newSize = Math.Max(8, AppearanceService.FontSize - 1);
            AppearanceService.SetFontSize(newSize);
            FontSizeDisplay.Text = $"{newSize:0}";
            AppearanceChanged?.Invoke();
        }

        private void FontSizeIncrease_Click(object sender, RoutedEventArgs e)
        {
            if (_suppressAppearance)
            {
                return;
            }

            var newSize = Math.Min(24, AppearanceService.FontSize + 1);
            AppearanceService.SetFontSize(newSize);
            FontSizeDisplay.Text = $"{newSize:0}";
            AppearanceChanged?.Invoke();
        }
    }
}

