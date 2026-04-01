using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Aire.AppLayer.Providers;
using Aire.Data;
using Aire.Providers;
using Aire.Services.Providers;
using Aire.UI.Settings.Models;

namespace Aire.UI
{
    public partial class SettingsWindow
    {
        private ProviderWorkflowCoordinator? _providerWorkflowCoordinator;
        private ProviderWorkflowCoordinator ProviderWorkflow => _providerWorkflowCoordinator ??= new ProviderWorkflowCoordinator(this);

        /// <summary>
        /// Owns the provider-editor workflow for the settings window.
        /// The window forwards events here, while shared provider rules live in ProviderConfigurationWorkflowService.
        /// </summary>
        private sealed class ProviderWorkflowCoordinator
        {
            private readonly SettingsWindow _owner;
            private readonly ProviderEditorApplicationService _editorService = new();
            private readonly ProviderEditorSaveApplicationService _saveService = new();

            public ProviderWorkflowCoordinator(SettingsWindow owner)
            {
                _owner = owner;
            }

            /// <summary>
            /// Loads the selected provider into the editor and refreshes any provider-specific model UI.
            /// </summary>
            public async Task HandleSelectionChangedAsync()
            {
                _owner._timeoutSaveTimer?.Stop();
                var selected = _owner.ProvidersListView.SelectedItem as Provider;
                if (selected == null)
                {
                    _owner.ClearForm();
                    return;
                }

                _owner._selectedProvider = selected;
                _owner._suppressAutoSave = true;
                _owner.ProviderLoadingBar.Visibility = Visibility.Visible;
                _owner.EditFormScrollViewer.IsEnabled = false;

                var plan = _editorService.BuildSelectionPlan(selected, _owner._isRefreshing);

                _owner.EditPanelTitle.Text = plan.Name;
                _owner.NameTextBox.Text = plan.Name;

                foreach (ComboBoxItem item in _owner.TypeComboBox.Items)
                {
                    if (item.Tag?.ToString() == plan.Type)
                    {
                        _owner.TypeComboBox.SelectedItem = item;
                        break;
                    }
                }

                _owner.ApiKeyPasswordBox.Password = plan.ApiKey;
                _owner.BaseUrlTextBox.Text = plan.BaseUrl;
                _owner.ModelComboBox.Text = plan.Model;
                _owner.SetTimeoutControlsFromProvider();
                _owner.EnabledCheckBox.IsChecked = plan.IsEnabled;
                _owner.ApplyProviderMetadata(plan.Metadata, hasKey: plan.HasApiKey);

                if (plan.ModelAction == ProviderEditorApplicationService.ModelLoadAction.LoadOllamaModels)
                {
                    // Start Ollama status check in background — the control shows its own state UI
                    _ = _owner.OllamaModelPicker.CheckAsync(plan.BaseUrl, plan.Model);
                }
                else if (plan.ModelAction == ProviderEditorApplicationService.ModelLoadAction.LoadMetadataModels)
                {
                    await _owner.PopulateModelsFromMetadataAsync(plan.Metadata);
                }
                else if (plan.ModelAction == ProviderEditorApplicationService.ModelLoadAction.SyncExistingOllamaItems)
                {
                    // Already in ready state — just pre-select the model
                    _owner.OllamaModelPicker.BaseUrl = plan.BaseUrl;
                    _owner.OllamaModelPicker.PreSelectModel(plan.Model);
                }

                _owner._suppressAutoSave = false;
                _owner.ProviderLoadingBar.Visibility = Visibility.Collapsed;
                _owner.EditFormScrollViewer.IsEnabled = true;
                _owner.SetFormEnabled(true);
                _owner.UpdateTimeoutSliderEnabledState();
                _owner.RunTestsButton.IsEnabled = true;
                _ = _owner.LoadAndDisplayTestResultsAsync(selected);
            }

            /// <summary>
            /// Applies the current editor fields back onto the selected provider and persists them.
            /// </summary>
            public async Task PerformAutoSaveAsync()
            {
                if (_owner._suppressAutoSave || _owner._selectedProvider == null)
                    return;

                _owner._timeoutSaveTimer?.Stop();

                var type = (_owner.TypeComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "OpenAI";
                string modelText, modelValue;
                IEnumerable<(string, string)>? modelMappings;

                if (type == "Ollama")
                {
                    modelText     = _owner.OllamaModelPicker.SelectedModelName ?? string.Empty;
                    modelValue    = _owner.OllamaModelPicker.SelectedModelName ?? string.Empty;
                    modelMappings = null;
                }
                else
                {
                    modelText     = _owner.ModelComboBox.Text;
                    modelValue    = _owner.ModelComboBox.SelectedValue as string ?? string.Empty;
                    modelMappings = _owner.ModelComboBox.ItemsSource is IEnumerable<OllamaModelItem> items
                        ? items.Select(i => (i.DisplayName, i.ModelName))
                        : null;
                }

                await _saveService.SaveAsync(
                    new ProviderEditorSaveApplicationService.SaveRequest(
                        _owner._selectedProvider,
                        _owner.NameTextBox.Text,
                        type,
                        _owner.ApiKeyPasswordBox.Password,
                        _owner.BaseUrlTextBox.Text,
                        modelText,
                        modelValue,
                        _owner.CurrentTimeoutMinutes,
                        _owner.EnabledCheckBox.IsChecked == true,
                        modelMappings),
                    _owner._databaseService);

                _owner.ProvidersChanged?.Invoke();
                await _owner.RefreshProvidersList(reSelectId: _owner._selectedProvider.Id);
            }
        }
    }
}
