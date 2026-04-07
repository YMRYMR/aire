using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Aire.AppLayer.Providers;
using Aire.Data;
using Aire.Providers;
using Aire.Services;
using Aire.Services.Providers;
using WpfComboBoxItem = System.Windows.Controls.ComboBoxItem;

namespace Aire.UI
{
    public partial class OnboardingWindow
    {
        private static readonly CodexActionApplicationService _codexActionApplicationService = new(new CodexManagementClient());

        internal void SkipForNow_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            AppState.SetHasCompletedOnboarding(true);
            Close();
            OpenSettingsAction?.Invoke();
        }

        private async void CodexInstallButton_Click(object sender, RoutedEventArgs e)
        {
            CodexInstallButton.IsEnabled = false;
            CodexInstallProgressBar.Visibility = Visibility.Visible;
            CodexInstallStatus.Text = LocalizationService.S("onboard.installingCodex", "Installing Codex CLI\u2026");

            try
            {
                var progress = new Progress<string>(message =>
                    Dispatcher.Invoke(() => CodexInstallStatus.Text = message));

                var result = await _codexActionApplicationService.InstallAsync(progress);
                CodexInstallStatus.Text = result.UserMessage;
            }
            finally
            {
                CodexInstallProgressBar.Visibility = Visibility.Collapsed;
                CodexInstallButton.IsEnabled = true;
                RefreshCodexInstallState();
            }
        }

        internal void RefreshCodexInstallState()
        {
            if (CodexSetupPanel == null)
                return;

            var status = CodexProvider.GetCliStatus();
            if (status.IsInstalled)
            {
                CodexInstallButton.Visibility = Visibility.Collapsed;
                CodexInstallProgressBar.Visibility = Visibility.Collapsed;
                CodexInstallStatus.Text = LocalizationService.S("onboard.codexDetected", "Codex CLI detected. You can test the connection now.");
                return;
            }

            CodexInstallButton.Visibility = Visibility.Visible;
            if (string.IsNullOrWhiteSpace(CodexInstallStatus.Text) ||
                CodexInstallStatus.Text.Contains("detected", StringComparison.OrdinalIgnoreCase))
            {
                CodexInstallStatus.Text = status.UserMessage;
            }
        }

        internal async void Step3Next_Click(object sender, RoutedEventArgs e)
        {
            if (ProviderTypeCombo.SelectedItem is WpfComboBoxItem typeItem &&
                !string.IsNullOrWhiteSpace(ProviderNameBox.Text))
            {
                var service = new OnboardingProviderSetupApplicationService();
                var type = typeItem.Tag as string ?? "OpenAI";
                try
                {
                    using var db = new DatabaseService();
                    await db.InitializeAsync();
                    var result = await service.CompleteStepAsync(
                        db,
                        new OnboardingProviderSetupApplicationService.Step3Request(
                            ProviderNameBox.Text,
                            type,
                            ApiKeyBox.Password,
                            BaseUrlBox.Text,
                            ModelCombo.SelectedValue as string
                                ?? (ModelCombo.SelectedItem as ModelDefinition)?.Id
                                ?? ModelCombo.Text.Trim(),
                            OllamaModelPicker.SelectedModelName,
                            _claudeSessionActive || ClaudeAiSession.Instance.IsReady));

                    if (result.IsDuplicate)
                    {
                        ConfirmationDialog.ShowAlert(this,
                            LocalizationService.S("onboard.alreadyConfigured", "Already configured"),
                            LocalizationService.S("onboard.alreadyConfigMsg", "This provider is already configured. Edit it in Settings."));
                        return;
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.Warn("OnboardingWindow", "Failed to check for duplicate provider during onboarding", ex);
                }
            }

            GoToStep(4);
        }
    }
}
