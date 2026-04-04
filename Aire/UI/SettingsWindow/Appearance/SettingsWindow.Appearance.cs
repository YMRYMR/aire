using System;
using System.Windows;
using System.Windows.Controls;
using Aire.Providers;
using Aire.Services;

namespace Aire.UI
{
    public partial class SettingsWindow
    {
        private void PopulateLanguageComboBox()
        {
            _suppressAppearance = true;
            LanguageComboBox.Items.Clear();
            foreach (var lang in LocalizationService.AvailableLanguages)
            {
                var flag = FlagPainter.Create(lang.Code, 22, 14);
                ((FrameworkElement)flag).Margin = new Thickness(0, 0, 7, 0);
                ((FrameworkElement)flag).VerticalAlignment = VerticalAlignment.Center;

                var nameText = new TextBlock { Text = lang.NativeName, VerticalAlignment = VerticalAlignment.Center };

                var panel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
                panel.Children.Add(flag);
                panel.Children.Add(nameText);

                var item = new ComboBoxItem { Tag = lang.Code, Content = panel };
                LanguageComboBox.Items.Add(item);
                if (lang.Code == LocalizationService.CurrentCode)
                {
                    LanguageComboBox.SelectedItem = item;
                }
            }

            _suppressAppearance = false;
        }

        private void OnLanguageChanged() => Dispatcher.Invoke(ApplyLocalization);

        private void ApplyLocalization()
        {
            var L = LocalizationService.S;

            TitleText.Text = L("settings.title", "Settings — Aire");
            CloseButton.ToolTip = L("tooltip.close", "Close");
            TabProviders.Header = L("settings.aiProviders", "AI Providers");
            TabAppearance.Header = L("settings.appearance", "Appearance");
            TabVoice.Header = L("settings.voiceOutput", "Voice");
            TabContext.Header = L("settings.context", "Context");
            TabAutoAccept.Header = L("settings.autoAccept", "Auto-accept");
            TabConnections.Header = L("settings.connections", "Connections");
            BrightnessLabel.Text = L("settings.brightness", "Brightness");
            ColorTintLabel.Text = L("settings.colorTint", "Color tint");
            NeutralLeftLabel.Text = L("settings.neutralLeft", "← Neutral");
            NeutralRightLabel.Text = L("settings.neutralRight", "Neutral →");
            AccentBrightnessLabel.Text = L("settings.accentBrightness", "Accent brightness");
            AccentTintLabel.Text = L("settings.accentTint", "Accent color");
            AccentNeutralLeftLabel.Text = L("settings.neutralLeft", "← Neutral");
            AccentNeutralRightLabel.Text = L("settings.neutralRight", "Neutral →");
            FontSizeLabel.Text = L("settings.fontSize", "Font size");
            LanguageLabel.Text = L("settings.language", "Language");
            ApiAccessTitle.Text = L("settings.apiAccessTitle", "Local API access");
            ApiAccessDescription.Text = L("settings.apiAccessDescription",
                "Allow trusted local apps to open Aire, read chat history, and request actions through the local API.");
            ApiAccessEnabledCheckBox.Content = L("settings.apiAccessEnabled", "Enable local API access");
            ApiAccessTokenTitle.Text = L("settings.apiAccessTokenTitle", "Auth token");
            ApiAccessTokenDescription.Text = L("settings.apiAccessTokenDescription",
                "Pass this token in local API requests to authorize control.");
            CopyApiAccessTokenButton.Content = L("settings.apiAccessTokenCopy", "Copy");
            RegenerateApiAccessTokenButton.Content = L("settings.apiAccessTokenRegenerate", "Regenerate");
            NameLabel.Text = L("settings.name", "Name");
            TypeLabel.Text = L("settings.type", "Type");
            ModelLabel.Text = L("settings.model", "Model");
            ApiKeyLabel.Text = L("settings.apiKey", "API Key");
            BaseUrlLabel.Text = L("settings.baseUrl", "Base URL (optional)");
            EnabledCheckBox.Content = L("settings.enabled", "Enabled");
            AddProviderButton.Content = L("settings.addProvider", "+ Add");
            SetupWizardButton.Content = L("settings.setupWizard", "Setup Wizard");
            AnthropicKeyHint.Text = L("settings.anthropicHint",
                "Tip: leave empty to use the ANTHROPIC_API_KEY environment variable.");
            ApiAccessEnabledCheckBox.ToolTip = L("settings.apiAccessEnabledTooltip",
                "Lets other local apps control Aire. Leave off unless you trust the caller.");

            VoiceLocalOnlyCheckBox.Content = L("settings.voiceLocalOnly", "Use local voices only (no internet required)");
            DownloadVoicesButton.Content = L("settings.downloadVoices", "Download Windows voices...");
            VoiceVoiceLabel.Text = L("settings.voice", "Voice");
            VoiceSpeedLabel.Text = L("settings.voiceSpeed", "Speed");
            TestVoiceButton.ToolTip = L("settings.testSelectedVoice", "Test selected voice");
            ContextDescriptionText.Text = L("settings.contextDescription", "Control how much conversation history Aire sends to providers and whether stable prompt prefixes can be marked cache-friendly.");
            EnablePromptCachingCheckBox.Content = L("settings.contextEnableCaching", "Prefer prompt caching when the provider supports it");
            EnableConversationSummariesCheckBox.Content = L("settings.contextEnableSummaries", "Summarize older trimmed conversation context");
            MaxMessagesLabel.Text = L("settings.contextMaxMessages", "Maximum messages in provider context");
            AnchorMessagesLabel.Text = L("settings.contextAnchorMessages", "Anchor messages kept from earlier in the conversation");
            UncachedRecentMessagesLabel.Text = L("settings.contextUncachedRecentMessages", "Most recent messages kept uncached");
            SummaryMaxCharactersLabel.Text = L("settings.contextSummaryMaxCharacters", "Maximum summary size (characters)");
            ContextHintText.Text = L("settings.contextHint", "Aire keeps system messages, a small anchored prefix, and the most recent turns. Older omitted turns can be summarized, and cached prefixes only help on providers that support prompt caching.");
            AutoAcceptProfileLabel.Text = L("settings.profile", "Profile");
            ApplyAutoAcceptProfileButton.Content = L("settings.apply", "Apply");
            SaveAutoAcceptProfileButton.Content = L("settings.saveAs", "Save as");
            DeleteAutoAcceptProfileButton.Content = L("settings.delete", "Delete");

            if (_selectedProvider == null)
            {
                EditPanelTitle.Text = L("settings.selectProvider", "Select a provider");
            }

            if (_selectedProvider != null)
            {
                var meta = ProviderFactory.GetMetadata(_selectedProvider.Type);
                ApplyProviderMetadata(meta, hasKey: !string.IsNullOrEmpty(_selectedProvider.ApiKey));
            }
        }

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

        private void CopyApiAccessTokenButton_Click(object sender, RoutedEventArgs e)
        {
            var token = AppState.GetApiAccessToken().Trim();
            if (string.IsNullOrWhiteSpace(token))
            {
                return;
            }

            System.Windows.Clipboard.SetText(token);
            ShowToast("API token copied to clipboard.");
        }

        private void RegenerateApiAccessTokenButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ConfirmationDialog.ShowCentered(this,
                title: "Regenerate API token?",
                message: "Existing local tools will stop working until they use the new token."))
            {
                return;
            }

            ApiAccessTokenBox.Text = AppState.RegenerateApiAccessToken();
            ShowToast("API token regenerated.");
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

