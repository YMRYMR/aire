using System.Windows;
using Aire.Providers;
using Aire.Services;

namespace Aire.UI
{
    public partial class SettingsWindow
    {
        private void ApplyProviderMetadata(IProviderMetadata meta, bool hasKey)
        {
            var hints = meta.FieldHints;

            ApiKeyLabel.Text = hints.ApiKeyRequired
                ? LocalizationService.S("settings.apiKey", "API Key")
                : LocalizationService.S("settings.apiKeyOptional", hints.ApiKeyLabel);
            ApiKeyLabel.Visibility = hints.ShowApiKey ? Visibility.Visible : Visibility.Collapsed;
            ApiKeyPasswordBox.Visibility = hints.ShowApiKey ? Visibility.Visible : Visibility.Collapsed;

            BaseUrlLabel.Visibility = hints.ShowBaseUrl ? Visibility.Visible : Visibility.Collapsed;
            BaseUrlTextBox.Visibility = hints.ShowBaseUrl ? Visibility.Visible : Visibility.Collapsed;
            UpdateOllamaModelPickerVisibility(meta.ProviderType);

            AnthropicKeyHint.Visibility = meta.ProviderType == "Anthropic"
                ? Visibility.Visible
                : Visibility.Collapsed;
            CodexInstallProgressBar.Visibility = Visibility.Collapsed;
            CodexInstallStatusText.Visibility = Visibility.Collapsed;

            foreach (var btn in ActionButtonMap.Values)
                btn.Visibility = Visibility.Collapsed;

            foreach (var action in meta.Actions)
            {
                if (!ActionButtonMap.TryGetValue(action.Id, out var btn))
                    continue;

                btn.Visibility = Visibility.Visible;

                if (action.Id == "claude-login")
                {
                    btn.Content = hasKey
                        ? LocalizationService.S("settings.reloginClaude", "Re-login with Claude.ai")
                        : LocalizationService.S("settings.loginClaude", "Login with Claude.ai");
                }
                else if (action.Id == "codex-install")
                {
                    var codexStatus = CodexProvider.GetCliStatus();
                    if (codexStatus.IsInstalled)
                    {
                        btn.Visibility = Visibility.Collapsed;
                        CodexInstallProgressBar.Visibility = Visibility.Collapsed;
                        CodexInstallStatusText.Visibility = Visibility.Visible;
                        CodexInstallStatusText.Text = "Codex CLI detected. You can test the connection now.";
                        continue;
                    }

                    btn.Content = "Install Codex CLI";
                    CodexInstallStatusText.Visibility = Visibility.Visible;
                    CodexInstallStatusText.Text = codexStatus.UserMessage;
                    if (!_isRefreshing)
                        btn.IsEnabled = true;
                }
            }
        }
    }
}
