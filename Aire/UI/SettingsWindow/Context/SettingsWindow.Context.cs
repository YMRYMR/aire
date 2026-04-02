using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using Aire.AppLayer.Chat;

namespace Aire.UI
{
    public partial class SettingsWindow
    {
        internal async Task LoadContextSettings()
        {
            _suppressContextSettings = true;
            try
            {
                var settings = await _contextSettingsApplicationService.LoadAsync();
                ApplyContextSettingsToControls(settings);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load context settings: {ex}");
            }
            finally
            {
                _suppressContextSettings = false;
            }
        }

        internal async Task SaveContextSettingsAsync()
        {
            if (_suppressContextSettings)
            {
                return;
            }

            try
            {
                var settings = ReadContextSettingsFromControls();
                await _contextSettingsApplicationService.SaveAsync(settings);
                ApplyContextSettingsToControls(settings);
                (Owner as Aire.MainWindow)?.ApplyContextWindowSettings(settings);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to save context settings: {ex}");
            }
        }

        private void ApplyContextSettingsToControls(ContextWindowSettings settings)
        {
            EnablePromptCachingCheckBox.IsChecked = settings.EnablePromptCaching;
            EnableConversationSummariesCheckBox.IsChecked = settings.EnableConversationSummaries;
            MaxMessagesTextBox.Text = settings.MaxMessages.ToString(CultureInfo.InvariantCulture);
            AnchorMessagesTextBox.Text = settings.AnchorMessages.ToString(CultureInfo.InvariantCulture);
            UncachedRecentMessagesTextBox.Text = settings.UncachedRecentMessages.ToString(CultureInfo.InvariantCulture);
            SummaryMaxCharactersTextBox.Text = settings.SummaryMaxCharacters.ToString(CultureInfo.InvariantCulture);
        }

        private ContextWindowSettings ReadContextSettingsFromControls()
        {
            var defaults = ContextWindowSettings.Default;

            return new ContextWindowSettings(
                MaxMessages: ParseOrDefault(MaxMessagesTextBox.Text, defaults.MaxMessages),
                AnchorMessages: ParseOrDefault(AnchorMessagesTextBox.Text, defaults.AnchorMessages),
                UncachedRecentMessages: ParseOrDefault(UncachedRecentMessagesTextBox.Text, defaults.UncachedRecentMessages),
                EnablePromptCaching: EnablePromptCachingCheckBox.IsChecked == true,
                EnableConversationSummaries: EnableConversationSummariesCheckBox.IsChecked == true,
                SummaryMaxCharacters: ParseOrDefault(SummaryMaxCharactersTextBox.Text, defaults.SummaryMaxCharacters));
        }

        private static int ParseOrDefault(string? raw, int fallback)
            => int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value
                : fallback;

        private void ContextSettingChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressContextSettings)
            {
                return;
            }

            _ = SaveContextSettingsAsync();
        }

        private void RestoreContextDefaults(object sender, RoutedEventArgs e)
        {
            _suppressContextSettings = true;
            ApplyContextSettingsToControls(ContextWindowSettings.Default);
            _suppressContextSettings = false;
            _ = SaveContextSettingsAsync();
        }
    }
}
