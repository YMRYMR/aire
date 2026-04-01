using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Aire.AppLayer.Providers;
using Aire.Data;
using Aire.Providers;
using Aire.Services;
using Aire.UI.Settings.Models;

namespace Aire.UI
{
    public partial class SettingsWindow
    {
        private async void SetupWizardButton_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Owner;
            mainWindow?.Hide();
            Hide();

            var wizard = new OnboardingWindow();
            wizard.ShowDialog();

            Show();
            mainWindow?.Show();

            ProvidersChanged?.Invoke();
            await RefreshProvidersList();
        }

        private async void AddProviderButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var newProvider = await ProviderListWorkflow.CreateDefaultProviderAsync(_databaseService);
                ProvidersChanged?.Invoke();
                await RefreshProvidersList(reSelectId: newProvider.Id);
            }
            catch (Exception ex)
            {
                ShowToast($"Could not add provider: {ex.Message}", isError: true);
            }
        }

        private async void ProvidersListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
            => await ProviderWorkflow.HandleSelectionChangedAsync();

        private async void TypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var type = (TypeComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "OpenAI";

            var meta = ProviderFactory.GetMetadata(type);

            if (_selectedProvider != null && !_suppressAutoSave)
            {
                if (type != "Ollama")
                    await PopulateModelsFromMetadataAsync(meta);
            }

            ApplyProviderMetadata(meta, hasKey: !string.IsNullOrEmpty(_selectedProvider?.ApiKey));
            UpdateTimeoutSliderEnabledState(_selectedProvider != null);
            await PerformAutoSave();
        }

        private async void DeleteListItem_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as System.Windows.Controls.Button;
            var provider = button?.Tag as Provider;
            if (provider == null) return;

            var title = LocalizationService.S("settings.deleteProviderTitle", "Delete Provider");
            var msg = LocalizationService.S("settings.deleteProviderMessage",
                "This will permanently remove the provider and its configuration.");

            if (ConfirmationDialog.ShowCentered(this, title: title, message: msg))
            {
                try
                {
                    await ProviderListWorkflow.DeleteProviderAsync(_databaseService, provider.Id);
                    ProvidersChanged?.Invoke();
                    await RefreshProvidersList();
                }
                catch (Exception ex)
                {
                    ShowToast($"Could not delete provider: {ex.Message}", isError: true);
                }
            }
        }

        private async void EnabledDot_Click(object sender, RoutedEventArgs e)
        {
            var border = sender as System.Windows.Controls.Border;
            var provider = border?.Tag as Provider;
            if (provider == null) return;

            provider.IsEnabled = !provider.IsEnabled;
            try
            {
                await _databaseService.UpdateProviderAsync(provider);
                ProvidersChanged?.Invoke();
                await RefreshProvidersList(reSelectId: provider.Id);
            }
            catch (Exception ex)
            {
                provider.IsEnabled = !provider.IsEnabled;
                ShowToast($"Could not toggle provider: {ex.Message}", isError: true);
            }
        }
    }
}

