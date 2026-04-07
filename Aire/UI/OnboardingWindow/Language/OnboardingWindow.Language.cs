using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using Aire.Providers;
using Aire.Services;
using WpfBrush = System.Windows.Media.Brush;
using WpfButton = System.Windows.Controls.Button;
using WpfColor = System.Windows.Media.Color;
using WpfComboBoxItem = System.Windows.Controls.ComboBoxItem;
using WpfFlowDir = System.Windows.FlowDirection;
using WpfOrientation = System.Windows.Controls.Orientation;
using WpfSolidBrush = System.Windows.Media.SolidColorBrush;
using WpfStackPanel = System.Windows.Controls.StackPanel;
using WpfTextBlock = System.Windows.Controls.TextBlock;
using WpfCursors = System.Windows.Input.Cursors;

namespace Aire.UI
{
    public partial class OnboardingWindow
    {
        private WpfButton? _selectedLangBtn;

        internal void BuildLanguageButtons()
        {
            foreach (var lang in LocalizationService.AvailableLanguages)
            {
                var btn = new WpfButton
                {
                    Tag = lang.Code,
                    Margin = new Thickness(3),
                    Padding = new Thickness(8, 5, 8, 5),
                    Cursor = WpfCursors.Hand,
                };

                var flag = FlagPainter.Create(lang.Code, 22, 14);
                ((FrameworkElement)flag).Margin = new Thickness(0, 0, 6, 0);
                ((FrameworkElement)flag).VerticalAlignment = VerticalAlignment.Center;

                var panel = new WpfStackPanel { Orientation = WpfOrientation.Horizontal };
                panel.Children.Add(flag);
                panel.Children.Add(new WpfTextBlock
                {
                    Text = lang.NativeName,
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center,
                });

                btn.Content = panel;
                btn.Click += LangButton_Click;
                LangPanel.Children.Add(btn);
            }
        }

        private void DetectAndSelectLanguage()
        {
            var stored = AppState.GetLanguage();
            var code = !string.IsNullOrEmpty(stored) ? stored
                : CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

            bool supported = LocalizationService.AvailableLanguages.Any(l => l.Code == code);
            if (!supported) code = "en";
            SelectLanguage(code);
        }

        private void LangButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is WpfButton btn && btn.Tag is string code)
                SelectLanguage(code);
        }

        internal void SelectLanguage(string code)
        {
            AppState.SetLanguage(code);
            LocalizationService.SetLanguage(code);

            foreach (WpfButton btn in LangPanel.Children)
            {
                bool isSelected = (string)btn.Tag == code;
                if (isSelected)
                {
                    btn.BorderBrush = (WpfBrush)FindResource("PrimaryBrush");
                    btn.BorderThickness = new Thickness(2);
                    _selectedLangBtn = btn;
                }
                else
                {
                    btn.BorderBrush = (WpfBrush)FindResource("BorderBrush");
                    btn.BorderThickness = new Thickness(1);
                }
            }

            ApplyLanguage();
        }

        private static string S(string key, string fallback)
            => LocalizationService.S(key, fallback);

        private void ApplyLanguage()
        {
            WelcomeTitle.Text = S("onboarding.welcomeTitle", "Welcome to Aire");
            WelcomeSubtitle.Text = S("onboarding.welcomeSubtitle", "AI for everyone");
            WelcomeDesc.Text = S("onboarding.welcomeDesc", "A tray-based AI app that lets you talk to OpenAI, ChatGPT, Anthropic Claude, Google AI, Ollama, and more in one place.");
            Step1NextButton.Content = S("onboarding.getStarted", "Let's get started  →");

            PickerTitle.Text = S("onboarding.pickerTitle", "Choose your AI provider");
            PickerSubtitle.Text = S("onboarding.pickerSubtitle", "Pick one to get started. You can add more anytime.");

            Step3Title.Text = S("onboarding.addAiTitle", "Add your first AI");
            Step3Subtitle.Text = S("onboarding.addAiSubtitle", "Connect to an AI provider to start chatting.");
            LabelProviderType.Text = S("onboarding.providerType", "Provider type");
            LabelName.Text = S("onboarding.name", "Name");
            LabelApiKey.Text = S("onboarding.apiKey", "API Key");
            LabelBaseUrl.Text = S("onboarding.baseUrl", "Base URL (optional — leave blank for OpenAI-compatible providers)");
            LabelModel.Text = S("onboarding.model", "Model");
            TestButton.Content = S("onboarding.testConnection", "Test connection");
            SkipLink.Text = S("onboarding.skipForNow", "Skip for now");
            Step3NextButton.Content = S("onboarding.finish", "Finish  →");

            RefreshApiKeyLink();
            UpdateVisitProviderButton();

            Step4Title.Text = S("onboarding.doneTitle", "You're all set!");
            Step4Desc.Text = S("onboarding.doneDesc", "Aire is running in your system tray. Click the icon any time to open the chat.");
            StartChattingButton.Content = S("onboarding.startChatting", "Start chatting");

            FlowDirection = LocalizationService.IsRightToLeftLanguage(LocalizationService.CurrentCode)
                ? WpfFlowDir.RightToLeft
                : WpfFlowDir.LeftToRight;
        }

        private void RefreshApiKeyLink()
        {
            if (ProviderTypeCombo.SelectedItem is not WpfComboBoxItem typeItem) return;
            var type = typeItem.Tag as string ?? "OpenAI";
            var descriptor = ProviderCatalog.TryGetDescriptor(type, out var resolved) ? resolved : null;
            var url = descriptor?.ApiKeyUrl;
            if (!string.IsNullOrEmpty(url))
            {
                ApiKeyLink.Text = S("onboarding.whereApiKey", "Where do I get an API key?");
                ApiKeyLink.Tag = url;
                ApiKeyLink.Visibility = Visibility.Visible;
            }
            else
            {
                ApiKeyLink.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateVisitProviderButton()
        {
            if (ProviderTypeCombo.SelectedItem is not WpfComboBoxItem typeItem) return;
            var type = typeItem.Tag as string ?? "OpenAI";
            var descriptor = ProviderCatalog.TryGetDescriptor(type, out var resolved) ? resolved : null;
            var apiKeyUrl = descriptor?.ApiKeyUrl;
            var signUpUrl = descriptor?.SignUpUrl;

            string? url = !string.IsNullOrEmpty(apiKeyUrl) ? apiKeyUrl
                : !string.IsNullOrEmpty(signUpUrl) ? signUpUrl
                : null;

            if (url != null)
            {
                string label = !string.IsNullOrEmpty(apiKeyUrl)
                    ? $"Get API key from {ProviderDisplayName(type)} →"
                    : $"Sign up for {ProviderDisplayName(type)} →";
                VisitProviderButton.Content = label;
                VisitProviderButton.Tag = url;
                VisitProviderButton.Visibility = Visibility.Visible;
            }
            else
            {
                VisitProviderButton.Visibility = Visibility.Collapsed;
            }
        }

        private void VisitProviderButton_Click(object sender, RoutedEventArgs e)
        {
            if (VisitProviderButton.Tag is string url && !string.IsNullOrEmpty(url))
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }

        private void RefreshClaudeSessionStatus()
        {
            bool hasSession = _claudeSessionActive || ClaudeAiSession.Instance.IsReady;
            if (hasSession)
            {
                ClaudeSessionStatus.Text = S("login.claudeLoggedIn", "\u2713 Logged in with Claude.ai");
                ClaudeSessionStatus.Foreground = new WpfSolidBrush(WpfColor.FromRgb(0x4C, 0xAF, 0x50));
                ClaudeSessionStatus.Visibility = Visibility.Visible;
                ClaudeLoginButton.Content = S("settings.reloginClaude", "Re-login with Claude.ai");
            }
            else
            {
                ClaudeSessionStatus.Visibility = Visibility.Collapsed;
                ClaudeLoginButton.Content = S("settings.loginClaude", "Login with Claude.ai");
            }
        }

        private void ClaudeLoginButton_Click(object sender, RoutedEventArgs e)
        {
            var loginWindow = new ClaudeAiLoginWindow();
            if (loginWindow.ShowDialog() == true)
            {
                _claudeSessionActive = true;
                RefreshClaudeSessionStatus();
                if (string.IsNullOrWhiteSpace(ApiKeyBox.Password))
                    ClearTestResult();
            }
        }
    }
}
