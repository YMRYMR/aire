using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Aire.Domain.Tools;

namespace Aire
{
    public partial class MainWindow
    {
        private System.Windows.Controls.ContextMenu? _toolsCategoryMenu;
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
                return;

            _toolsCategoryMenu = new System.Windows.Controls.ContextMenu();

            var enableAllItem = new System.Windows.Controls.MenuItem { Header = "Enable all tool categories" };
            enableAllItem.Click += async (_, _) =>
            {
                _enabledToolCategories = ToolCategoryCatalog.AllEnabled();
                await PersistToolCategoriesAsync();
            };
            _toolsCategoryMenu.Items.Add(enableAllItem);

            _noToolsMenuItem = new System.Windows.Controls.MenuItem
            {
                Header = "No tools",
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
                    Header = option.Label,
                    ToolTip = option.Description,
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
        }

        private void RefreshToolsCategoryMenuChecks()
        {
            if (_toolsCategoryMenu == null)
                return;

            foreach (var item in _toolsCategoryMenu.Items.OfType<System.Windows.Controls.MenuItem>())
            {
                if (item.Tag is string category)
                    item.IsChecked = _enabledToolCategories.Contains(category);
            }

            if (_noToolsMenuItem != null)
                _noToolsMenuItem.IsChecked = _enabledToolCategories.Count == 0;
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
                .Select(option => option.Label)
                .ToList();

            ToolsButton.ToolTip = enabledLabels.Count == 0
                ? "Tools disabled — click to choose tool categories"
                : $"Enabled tool categories: {string.Join(", ", enabledLabels)}";

            if (enabledLabels.Count > 0)
                ToolsButton.ClearValue(System.Windows.Controls.Control.OpacityProperty);
            else
                ToolsButton.Opacity = 0.4;
        }
    }
}
