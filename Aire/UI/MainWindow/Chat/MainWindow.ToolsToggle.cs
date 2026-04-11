using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Aire.Domain.Tools;
using Aire.Services;

namespace Aire
{
    public partial class MainWindow
    {
        private System.Windows.Controls.ContextMenu? _toolsCategoryMenu;
        private System.Windows.Controls.MenuItem? _enableAllToolsMenuItem;
        private System.Windows.Controls.MenuItem? _noToolsMenuItem;

        private bool ToolsEnabled => _enabledToolCategories.Count > 0;

        private void ToolsButton_Click(object sender, RoutedEventArgs e)
        {
            EnsureToolsCategoryMenu();
            RefreshToolsCategoryMenuChecks();
            _toolsCategoryMenu!.PlacementTarget = ToolsButton;
            _toolsCategoryMenu.IsOpen = true;
        }

        private void EnsureToolsCategoryMenu()
        {
            if (_toolsCategoryMenu != null)
            {
                RefreshToolsCategoryMenuLocalization();
                return;
            }

            _toolsCategoryMenu = new System.Windows.Controls.ContextMenu();

            _enableAllToolsMenuItem = new System.Windows.Controls.MenuItem();
            _enableAllToolsMenuItem.Click += async (_, _) =>
            {
                _enabledToolCategories = ToolCategoryCatalog.AllEnabled();
                await PersistToolCategoriesAsync();
            };
            _toolsCategoryMenu.Items.Add(_enableAllToolsMenuItem);

            _noToolsMenuItem = new System.Windows.Controls.MenuItem
            {
                IsCheckable = true,
            };
            _noToolsMenuItem.Click += async (_, _) =>
            {
                _enabledToolCategories.Clear();
                await PersistToolCategoriesAsync();
            };
            _toolsCategoryMenu.Items.Add(_noToolsMenuItem);
            _toolsCategoryMenu.Items.Add(new Separator());

            foreach (var option in ToolCategoryCatalog.Options)
            {
                var item = new System.Windows.Controls.MenuItem
                {
                    IsCheckable = true,
                    Tag = option.Id,
                    StaysOpenOnClick = true,
                };
                item.Click += async (_, _) =>
                {
                    if (item.IsChecked)
                        _enabledToolCategories.Add(option.Id);
                    else
                        _enabledToolCategories.Remove(option.Id);

                    await PersistToolCategoriesAsync();
                    RefreshToolsCategoryMenuChecks();
                };
                _toolsCategoryMenu.Items.Add(item);
            }

            RefreshToolsCategoryMenuLocalization();
        }

        internal void RefreshToolsCategoryMenuLocalization()
        {
            if (_toolsCategoryMenu == null)
                return;

            if (_enableAllToolsMenuItem != null)
                _enableAllToolsMenuItem.Header = LocalizationService.S("tools.enableAllCategories", "Enable all tool categories");

            if (_noToolsMenuItem != null)
                _noToolsMenuItem.Header = LocalizationService.S("tools.noTools", "No tools");

            foreach (var item in _toolsCategoryMenu.Items.OfType<System.Windows.Controls.MenuItem>())
            {
                if (item.Tag is not string categoryId)
                    continue;

                var option = ToolCategoryCatalog.Options.FirstOrDefault(option => string.Equals(option.Id, categoryId, StringComparison.OrdinalIgnoreCase));
                if (option == null)
                    continue;

                item.Header = LocalizationService.S($"toolCategory.{categoryId}.label", option.Label);
                item.ToolTip = LocalizationService.S($"toolCategory.{categoryId}.description", option.Description);
            }
        }

        private void RefreshToolsCategoryMenuChecks()
        {
            if (_toolsCategoryMenu == null)
                return;

            var supported = _toolsSupportedByProvider;

            foreach (var item in _toolsCategoryMenu.Items.OfType<System.Windows.Controls.MenuItem>())
            {
                item.IsEnabled = supported;
                if (item.Tag is string category)
                    item.IsChecked = supported && _enabledToolCategories.Contains(category);
            }

            if (_noToolsMenuItem != null)
            {
                _noToolsMenuItem.IsEnabled = supported;
                _noToolsMenuItem.IsChecked = supported && _enabledToolCategories.Count == 0;
            }

            if (_enableAllToolsMenuItem != null)
                _enableAllToolsMenuItem.IsEnabled = supported;
        }

        private async Task PersistToolCategoriesAsync()
        {
            await _toolCategorySettingsApplicationService.SaveAsync(_enabledToolCategories);
            UpdateToolsButtonState();
        }

        private void UpdateToolsButtonState()
        {
            var enabledLabels = ToolCategoryCatalog.Options
                .Where(option => _enabledToolCategories.Contains(option.Id))
                .Select(option => LocalizationService.S($"toolCategory.{option.Id}.label", option.Label))
                .ToList();

            if (!_toolsSupportedByProvider)
            {
                ToolsButton.ToolTip = LocalizationService.S(
                    "tooltip.toolsNotSupported",
                    "This model does not support tool calling");
                ToolsButton.Opacity = 0.4;
                return;
            }

            ToolsButton.ToolTip = enabledLabels.Count == 0
                ? LocalizationService.S("tooltip.toolsDisabled", "Tools disabled — click to choose tool categories")
                : string.Format(
                    LocalizationService.S("tooltip.toolsEnabled", "Enabled tool categories: {0}"),
                    string.Join(", ", enabledLabels));

            if (enabledLabels.Count > 0)
                ToolsButton.ClearValue(System.Windows.Controls.Control.OpacityProperty);
            else
                ToolsButton.Opacity = 0.4;
        }
    }
}
