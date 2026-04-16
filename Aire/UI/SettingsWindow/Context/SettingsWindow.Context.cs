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

                var customInstructions = await _customInstructionsService.LoadAsync();
                CustomInstructionsTextBox.Text = customInstructions;
            }
            catch
            {
                Debug.WriteLine("Failed to load context settings.");
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
                await _customInstructionsService.SaveAsync(CustomInstructionsTextBox.Text ?? string.Empty);
                ApplyContextSettingsToControls(settings);
                (Owner as Aire.MainWindow)?.ApplyContextWindowSettings(settings);
                (Owner as Aire.MainWindow)?.ApplyCustomInstructions(CustomInstructionsTextBox.Text ?? string.Empty);
            }
            catch
            {
                Debug.WriteLine("Failed to save context settings.");
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
            EnableTokenAwareTruncationCheckBox.IsChecked = settings.EnableTokenAwareTruncation;
            MaxTokensTextBox.Text = settings.MaxTokens?.ToString(CultureInfo.InvariantCulture) ?? "";
            AnchorTokensTextBox.Text = settings.AnchorTokens.ToString(CultureInfo.InvariantCulture);
            TailTokensTextBox.Text = settings.TailTokens.ToString(CultureInfo.InvariantCulture);
            EnableToolFocusWindowCheckBox.IsChecked = settings.EnableToolFocusWindow;
            EnableRetryFollowUpWindowCheckBox.IsChecked = settings.EnableRetryFollowUpWindow;
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
                SummaryMaxCharacters: ParseOrDefault(SummaryMaxCharactersTextBox.Text, defaults.SummaryMaxCharacters),
                MaxTokens: ParseNullableOrDefault(MaxTokensTextBox.Text, defaults.MaxTokens),
                AnchorTokens: ParseOrDefault(AnchorTokensTextBox.Text, defaults.AnchorTokens),
                TailTokens: ParseOrDefault(TailTokensTextBox.Text, defaults.TailTokens),
                EnableTokenAwareTruncation: EnableTokenAwareTruncationCheckBox.IsChecked == true,
                EnableToolFocusWindow: EnableToolFocusWindowCheckBox.IsChecked == true,
                EnableRetryFollowUpWindow: EnableRetryFollowUpWindowCheckBox.IsChecked == true);
        }

        private static int ParseOrDefault(string? raw, int fallback)
            => int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                ? value
                : fallback;

        private static int? ParseNullableOrDefault(string? raw, int? fallback)
            => string.IsNullOrWhiteSpace(raw) ? fallback : int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? (int?)value : fallback;

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
            CustomInstructionsTextBox.Text = string.Empty;
            _suppressContextSettings = false;
            _ = SaveContextSettingsAsync();
        }
    }
}
