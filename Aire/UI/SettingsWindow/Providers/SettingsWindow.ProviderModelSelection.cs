using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Aire.AppLayer.Providers;
using Aire.Providers;
using Aire.UI.Controls;
using Aire.UI.Settings.Models;

namespace Aire.UI
{
    public partial class SettingsWindow
    {
        internal async Task PopulateModelsFromMetadataAsync(IProviderMetadata meta)
        {
            if (_selectedProvider == null) return;

            bool AreSameModels(IEnumerable<OllamaModelItem> a, IEnumerable<OllamaModelItem> b)
            {
                var aIds = a.Select(item => item.ModelName).OrderBy(id => id).ToList();
                var bIds = b.Select(item => item.ModelName).OrderBy(id => id).ToList();
                return aIds.SequenceEqual(bIds);
            }

            bool IsSubset(IEnumerable<OllamaModelItem> subset, IEnumerable<OllamaModelItem> superset)
            {
                var supersetIds = superset.Select(item => item.ModelName).ToHashSet();
                return subset.All(item => supersetIds.Contains(item.ModelName));
            }

            var catalog = await new ProviderModelCatalogApplicationService().LoadModelsAsync(
                meta,
                ApiKeyPasswordBox.Password,
                _selectedProvider.BaseUrl,
                default);

            var defaultItems = catalog.DefaultModels
                .Select(d => new OllamaModelItem { DisplayName = d.DisplayName, ModelName = d.Id })
                .ToList();

            var existingItems = ModelComboBox.ItemsSource as IEnumerable<OllamaModelItem>;
            if (existingItems == null || !AreSameModels(existingItems, defaultItems))
            {
                bool wasOpen = ModelComboBox.IsDropDownOpen;
                ModelComboBox.ItemsSource = defaultItems;
                if (wasOpen)
                    ModelComboBox.IsDropDownOpen = true;
            }

            if (!string.IsNullOrEmpty(_selectedProvider.Model))
                SelectModelInComboBox(_selectedProvider.Model);
            if (!catalog.UsedLiveModels)
            {
                if (!string.IsNullOrWhiteSpace(ApiKeyPasswordBox.Password))
                    Debug.WriteLine($"Live model fetch fell back to built-in list for {meta.ProviderType}.");
                return;
            }

            var liveItems = catalog.EffectiveModels
                .Select(d => new OllamaModelItem { DisplayName = d.DisplayName, ModelName = d.Id })
                .ToList();

            var currentItems = ModelComboBox.ItemsSource as IEnumerable<OllamaModelItem>;
            bool shouldReplace = currentItems == null
                || (!AreSameModels(currentItems, liveItems) && !IsSubset(liveItems, currentItems));

            if (shouldReplace)
            {
                bool wasOpen = ModelComboBox.IsDropDownOpen;
                ModelComboBox.ItemsSource = liveItems;
                if (wasOpen)
                    ModelComboBox.IsDropDownOpen = true;
                Debug.WriteLine($"Live model list replaced ItemsSource (count: {liveItems.Count})");
            }
            else if (currentItems != null && IsSubset(liveItems, currentItems))
            {
                Debug.WriteLine("Live model list is a subset of current list; keeping current list to prevent dropdown flicker.");
            }
            else
            {
                Debug.WriteLine("Live model list not replaced (subset or identical)");
            }

            if (!string.IsNullOrEmpty(_selectedProvider.Model))
                SelectModelInComboBox(_selectedProvider.Model);
        }

        private void SelectModelInComboBox(string modelName)
        {
            if (ModelComboBox.ItemsSource is not IEnumerable<OllamaModelItem> items) return;
            var match = items.FirstOrDefault(i => i.ModelName == modelName);
            if (match != null)
                ModelComboBox.SelectedItem = match;
            else
                ModelComboBox.Text = modelName;
        }

        private async void ModelComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateTimeoutSliderEnabledState(_selectedProvider != null);

            if (_suppressAutoSave || _selectedProvider == null) return;

            await PerformAutoSave();
        }

        private void OnModelComboBoxTextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateTimeoutSliderEnabledState(_selectedProvider != null);
            EditableComboBoxFilterHelper.ApplyFilter(
                ModelComboBox,
                _suppressModelFilter,
                ref _preFilterSelection,
                item => item.ModelName,
                item => item.DisplayName);
        }

        private void ModelComboBox_DropDownClosed(object? sender, EventArgs e)
        {
            EditableComboBoxFilterHelper.ResetFilter(
                ModelComboBox,
                ref _suppressModelFilter,
                ref _preFilterSelection,
                item => string.IsNullOrEmpty(item.SizeStr)
                    ? item.ModelName
                    : $"{item.ModelName}  ({item.SizeStr})");
            UpdateTimeoutSliderEnabledState(_selectedProvider != null);
        }
    }
}
