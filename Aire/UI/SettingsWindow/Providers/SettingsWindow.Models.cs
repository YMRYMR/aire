using System;
using System.Windows;
using Aire.AppLayer.Providers;
using Aire.Providers;
using Aire.Services;

namespace Aire.UI
{
    public partial class SettingsWindow
    {
        private readonly ProviderFormActionsApplicationService _providerFormActions = new();

        internal async void RefreshModelsButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedProvider == null) return;

            try
            {
                var meta = ProviderFactory.GetMetadata(_selectedProvider.Type);
                await PopulateModelsFromMetadataAsync(meta);
                ShowToast(LocalizationService.S("toast.modelRefreshed", "Model list refreshed from server."));
            }
            catch
            {
                ShowToast(LocalizationService.S("toast.modelRefreshFail", "Could not refresh models."), isError: true);
            }
        }

        private async void ImportModelsButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Import Model Definitions",
                Filter = "JSON files (*.json)|*.json",
                Multiselect = false,
            };

            if (dialog.ShowDialog(this) != true) return;

            try
            {
                _providerFormActions.ImportModels(dialog.FileName);
                ShowToast(LocalizationService.S("toast.modelsImported", "Models imported. Refreshing list\u2026"));

                if (_selectedProvider == null) return;

                await PopulateModelsFromMetadataAsync(ProviderFactory.GetMetadata(_selectedProvider.Type));
            }
            catch
            {
                ShowToast(LocalizationService.S("toast.importFailed", "Import failed."), isError: true);
            }
        }

        internal async void InstallOllamaButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedProvider == null) return;

            var toolStatus = _providerFormActions.GetProviderToolStatus(_selectedProvider.Type);
            if (toolStatus?.IsInstalled == true)
            {
                ShowToast(toolStatus.StatusMessage);
                return;
            }

            if (string.Equals(_selectedProvider.Type, "Codex", StringComparison.OrdinalIgnoreCase))
            {
                if (!ConfirmationDialog.ShowCentered(
                        this,
                        title: LocalizationService.S("settings.installCodex", "Install Codex CLI"),
                        message: LocalizationService.S("settings.installCodexMsg", "This will run 'npm install -g @openai/codex' on your system. Continue?")))
                {
                    return;
                }

                InstallOllamaButton.IsEnabled = false;
                CodexInstallProgressBar.Visibility = Visibility.Visible;
                CodexInstallStatusText.Visibility = Visibility.Visible;
                CodexInstallStatusText.Text = LocalizationService.S("onboard.installingCodex", "Installing Codex CLI\u2026");
                var progress = new Progress<string>(message => Dispatcher.Invoke(() =>
                {
                    CodexInstallStatusText.Text = message;
                }));
                var result = await _providerFormActions.InstallProviderToolAsync(_selectedProvider.Type, progress);
                CodexInstallProgressBar.Visibility = Visibility.Collapsed;
                CodexInstallStatusText.Text = result.UserMessage;
                ApplyProviderMetadata(ProviderFactory.GetMetadata(_selectedProvider.Type), !string.IsNullOrWhiteSpace(ApiKeyPasswordBox.Password));
                InstallOllamaButton.IsEnabled = true;
                ShowToast(result.UserMessage, isError: !result.Succeeded);
            }
        }
    }
}
