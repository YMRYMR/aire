using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Aire.AppLayer.Providers;
using Aire.Data;
using Aire.Domain.Providers;
using Aire.Providers;
using Aire.Services;
using Aire.Services.Providers;
using Aire.UI.Settings.Models;

namespace Aire.UI
{
    public partial class SettingsWindow
    {
        private ProviderModelCoordinator? _providerModelCoordinator;
        private ProviderModelCoordinator ProviderModels => _providerModelCoordinator ??= new ProviderModelCoordinator(this);

        internal async void RefreshModelsButton_Click(object sender, RoutedEventArgs e)
            => await ProviderModels.RefreshModelsAsync();

        private async void ImportModelsButton_Click(object sender, RoutedEventArgs e)
            => await ProviderModels.ImportModelsAsync();

        internal async void InstallOllamaButton_Click(object sender, RoutedEventArgs e)
            => await ProviderModels.InstallProviderToolAsync();

        private sealed class ProviderModelCoordinator
        {
            private readonly SettingsWindow _owner;

            public ProviderModelCoordinator(SettingsWindow owner)
            {
                _owner = owner;
            }

            public async Task RefreshModelsAsync()
            {
                if (_owner._selectedProvider == null) return;

                try
                {
                    var meta = ProviderFactory.GetMetadata(_owner._selectedProvider.Type);
                    await _owner.PopulateModelsFromMetadataAsync(meta);
                    _owner.ShowToast("Model list refreshed from server.");
                }
                catch (Exception ex)
                {
                    _owner.ShowToast($"Could not refresh models: {ex.Message}", isError: true);
                }
            }

            public async Task ImportModelsAsync()
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Import Model Definitions",
                    Filter = "JSON files (*.json)|*.json",
                    Multiselect = false,
                };

                if (dialog.ShowDialog(_owner) != true) return;

                try
                {
                    ModelCatalog.ImportFile(dialog.FileName);
                    _owner.ShowToast("Models imported. Refreshing list…");

                    if (_owner._selectedProvider == null) return;

                    await _owner.PopulateModelsFromMetadataAsync(ProviderFactory.GetMetadata(_owner._selectedProvider.Type));
                }
                catch (Exception ex)
                {
                    _owner.ShowToast($"Import failed: {ex.Message}", isError: true);
                }
            }

            public async Task InstallProviderToolAsync()
            {
                if (_owner._selectedProvider == null) return;

                if (_owner._selectedProvider.Type == "Codex")
                {
                    if (CodexProvider.HasLaunchableCli())
                    {
                        _owner.ShowToast("Codex CLI is already installed.");
                        return;
                    }

                    if (!ConfirmationDialog.ShowCentered(
                            _owner,
                            title: "Install Codex CLI",
                            message: "This will run 'npm install -g @openai/codex' on your system. Continue?"))
                    {
                        return;
                    }

                    var codexService = new CodexActionApplicationService(new CodexManagementClient());
                    _owner.InstallOllamaButton.IsEnabled = false;
                    _owner.CodexInstallProgressBar.Visibility = Visibility.Visible;
                    _owner.CodexInstallStatusText.Visibility = Visibility.Visible;
                    _owner.CodexInstallStatusText.Text = "Installing Codex CLI…";
                    var progress = new Progress<string>(message => _owner.Dispatcher.Invoke(() =>
                    {
                        _owner.CodexInstallStatusText.Text = message;
                    }));
                    var result = await codexService.InstallAsync(progress);
                    _owner.CodexInstallProgressBar.Visibility = Visibility.Collapsed;
                    _owner.CodexInstallStatusText.Text = result.UserMessage;
                    _owner.ApplyProviderMetadata(ProviderFactory.GetMetadata(_owner._selectedProvider.Type), !string.IsNullOrWhiteSpace(_owner.ApiKeyPasswordBox.Password));
                    _owner.InstallOllamaButton.IsEnabled = true;
                    _owner.ShowToast(result.UserMessage, isError: !result.Succeeded);
                }
            }
        }
    }
}
