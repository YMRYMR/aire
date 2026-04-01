using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using Aire.Data;
using Aire.Services;

namespace Aire.UI
{
    public partial class SettingsWindow
    {
        private void ClearForm()
        {
            _timeoutSaveTimer?.Stop();
            _selectedProvider = null;
            ProvidersListView.SelectedItem = null;
            EditPanelTitle.Text = LocalizationService.S("settings.selectProvider", "Select a provider");
            _suppressAutoSave = true;
            NameTextBox.Text = "";
            TypeComboBox.SelectedIndex = -1;
            ApiKeyPasswordBox.Password = "";
            BaseUrlTextBox.Text = "";
            ModelComboBox.Text = "";
            OllamaModelPicker.CancelCheck();
            OllamaModelPicker.Visibility  = System.Windows.Visibility.Collapsed;
            ModelLabel.Visibility         = System.Windows.Visibility.Visible;
            NonOllamaModelArea.Visibility  = System.Windows.Visibility.Visible;
            TimeoutSlider.Value = TimeoutMinutesToSliderValue(Provider.DefaultTimeoutMinutes);
            TimeoutValueLabel.Text = FormatTimeout(5);
            EnabledCheckBox.IsChecked = false;
            _suppressAutoSave = false;
            SetFormEnabled(false);
        }

        private void SetFormEnabled(bool enabled)
        {
            // Right pane — form fields
            NameTextBox.IsEnabled          = enabled;
            TypeComboBox.IsEnabled         = enabled;
            OllamaModelPicker.IsEnabled    = enabled;
            ModelComboBox.IsEnabled        = enabled;
            RefreshModelsButton.IsEnabled  = enabled;
            ApiKeyPasswordBox.IsEnabled    = enabled;
            ClaudeAiLoginButton.IsEnabled  = enabled;
            InstallOllamaButton.IsEnabled  = enabled;
            BaseUrlTextBox.IsEnabled       = enabled;
            EnabledCheckBox.IsEnabled      = enabled;
            UpdateTimeoutSliderEnabledState(enabled);

            // RunTestsButton is gated on a provider being selected — only restore it when re-enabling
            if (enabled)
                RunTestsButton.IsEnabled = true;
            // StopTestsButton is never touched here; the test runner manages it directly
        }

        /// <summary>
        /// Locks/unlocks the entire providers tab during a capability test run.
        /// The left pane is blocked via IsHitTestVisible + opacity rather than IsEnabled,
        /// so WPF's disabled styling (which turns the list white) is never triggered.
        /// </summary>
        internal void SetProvidersTabEnabled(bool enabled)
        {
            // Left pane: block interaction without triggering WPF disabled appearance
            ProviderListPaneControl.IsHitTestVisible = enabled;
            ProviderListPaneControl.Opacity          = enabled ? 1.0 : 0.4;

            SetFormEnabled(enabled);
        }

        internal void UpdateOllamaModelPickerVisibility(string providerType)
        {
            bool isOllama = providerType == "Ollama";
            OllamaModelPicker.Visibility  = isOllama ? Visibility.Visible : Visibility.Collapsed;
            ModelLabel.Visibility         = isOllama ? Visibility.Collapsed : Visibility.Visible;
            NonOllamaModelArea.Visibility  = isOllama ? Visibility.Collapsed : Visibility.Visible;
            if (!isOllama)
                OllamaModelPicker.CancelCheck();
        }

        internal void UpdateTimeoutSliderEnabledState(bool formEnabled = true)
        {
            if (ModelComboBox == null || TimeoutSlider == null)
            {
                return;
            }

            var selectedValue = ModelComboBox.SelectedValue as string;
            var typedValue = ModelComboBox.Text?.Trim();
            var hasModel = !string.IsNullOrWhiteSpace(selectedValue) || !string.IsNullOrWhiteSpace(typedValue);
            TimeoutSlider.IsEnabled = formEnabled && hasModel;
        }

        private void TimeoutSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateTimeoutLabelFromSlider();
            if (_suppressAutoSave || _selectedProvider == null)
            {
                return;
            }

            if (_timeoutSaveTimer == null)
            {
                _timeoutSaveTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(350)
                };
                _timeoutSaveTimer.Tick += async (_, _) =>
                {
                    _timeoutSaveTimer?.Stop();
                    await PerformAutoSave();
                };
            }

            _timeoutSaveTimer.Stop();
            _timeoutSaveTimer.Start();
        }

        private int CurrentTimeoutMinutes => TimeoutSliderValueToMinutes(TimeoutSlider.Value);

        private void SetTimeoutControlsFromProvider()
        {
            var minutes = Math.Clamp(_selectedProvider?.TimeoutMinutes ?? Provider.DefaultTimeoutMinutes, MinTimeoutMinutes, MaxTimeoutMinutes);
            TimeoutSlider.Value = TimeoutMinutesToSliderValue(minutes);
            TimeoutValueLabel.Text = FormatTimeout(minutes);
        }

        private void UpdateTimeoutLabelFromSlider() => TimeoutValueLabel.Text = FormatTimeout(CurrentTimeoutMinutes);

        private static double TimeoutMinutesToSliderValue(int minutes)
        {
            var clamped = Math.Clamp(minutes, MinTimeoutMinutes, MaxTimeoutMinutes);
            return Math.Log(clamped) / Math.Log(MaxTimeoutMinutes);
        }

        private static int TimeoutSliderValueToMinutes(double sliderValue)
        {
            var normalized = Math.Clamp(sliderValue, 0.0, 1.0);
            var minutes = (int)Math.Round(Math.Pow(MaxTimeoutMinutes, normalized));
            return Math.Clamp(minutes, MinTimeoutMinutes, MaxTimeoutMinutes);
        }

        private static string FormatTimeout(int minutes)
        {
            if (minutes >= 43200)
            {
                return "1 month";
            }

            if (minutes < 60)
            {
                return minutes == 1 ? "1 minute" : $"{minutes} minutes";
            }

            if (minutes < 24 * 60)
            {
                var hours = minutes / 60.0;
                return hours == 1 ? "1 hour" : $"{hours:0.#} hours";
            }

            var days = minutes / 1440.0;
            return days == 1 ? "1 day" : $"{days:0.#} days";
        }
    }
}
