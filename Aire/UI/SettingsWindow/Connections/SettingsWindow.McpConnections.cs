using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Aire.AppLayer.Mcp;
using Aire.Services.Mcp;
using Aire.UI.Settings.Models;
using MessageBox = System.Windows.MessageBox;

namespace Aire.UI
{
    public partial class SettingsWindow
    {
        private void McpServersList_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

        private void McpTemplatesBtn_Click(object sender, RoutedEventArgs e)
        {
            McpTemplatesMenu.PlacementTarget = McpTemplatesBtn;
            McpTemplatesMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            McpTemplatesMenu.IsOpen = true;
        }

        private void AddMcpBtn_Click(object sender, RoutedEventArgs e)
        {
            McpNameBox.Text = McpCommandBox.Text = McpArgsBox.Text = McpWorkDirBox.Text = McpEnvVarsBox.Text = string.Empty;
            McpEditTitle.Text = Services.LocalizationService.S("mcp.addTitle", "Add MCP server");
            _editingMcpVm = null;
            McpEditPanel.Visibility = Visibility.Visible;
        }

        private async void CatalogMcpActionBtn_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as System.Windows.Controls.Button)?.Tag is not McpCatalogEntryViewModel vm)
                return;

            var installed = _mcpCatalogApplicationService.FindInstalledConfig(vm.Key, _mcpVms.Select(entry => entry.Model));
            if (installed != null)
            {
                if (MessageBox.Show(string.Format(Services.LocalizationService.S("mcp.removeConfirm", "Remove '{0}'?"), installed.Name), Services.LocalizationService.S("confirm.title", "Confirm"), MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                    return;

                McpManager.Instance.StopSingle(installed.Name);
                await _mcpConfigApplicationService.DeleteMcpServerAsync(installed.Id);

                var existingVm = _mcpVms.FirstOrDefault(entry => entry.Model.Id == installed.Id);
                if (existingVm != null)
                    _mcpVms.Remove(existingVm);

                RefreshMcpCatalogState(_mcpVms.Select(entry => entry.Model).ToList());
                return;
            }

            var config = _mcpCatalogApplicationService.BuildConfig(vm.Key);
            var id = await _mcpConfigApplicationService.InsertMcpServerAsync(config);
            config.Id = id;
            var newVm = new McpServerViewModel(config);
            _mcpVms.Add(newVm);

            try
            {
                await McpManager.Instance.StartSingleAsync(config);
                newVm.RefreshStatus();
            }
            catch
            {
                newVm.StatusColor = "#CC3333";
            }

            RefreshMcpCatalogState(_mcpVms.Select(entry => entry.Model).ToList());
        }

        private void EditMcpBtn_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as System.Windows.Controls.Button)?.Tag is not McpServerViewModel vm)
            {
                return;
            }

            _editingMcpVm = vm;
            McpEditTitle.Text = string.Format(Services.LocalizationService.S("mcp.editTitle", "Edit {0}"), vm.Name);
            McpNameBox.Text = vm.Model.Name;
            McpCommandBox.Text = vm.Model.Command;
            McpArgsBox.Text = vm.Model.Arguments;
            McpWorkDirBox.Text = vm.Model.WorkingDirectory;
            McpEnvVarsBox.Text = string.Join("\n", vm.Model.EnvVars.Select(kv => $"{kv.Key}={kv.Value}"));
            McpTestResult.Text = string.Empty;
            McpEditPanel.Visibility = Visibility.Visible;
        }

        private async void DeleteMcpBtn_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as System.Windows.Controls.Button)?.Tag is not McpServerViewModel vm)
            {
                return;
            }

            if (MessageBox.Show(string.Format(Services.LocalizationService.S("mcp.removeConfirm", "Remove '{0}'?"), vm.Name), Services.LocalizationService.S("confirm.title", "Confirm"), MessageBoxButton.YesNo) != MessageBoxResult.Yes)
            {
                return;
            }

            McpManager.Instance.StopSingle(vm.Model.Name);
            await _mcpConfigApplicationService.DeleteMcpServerAsync(vm.Model.Id);
            _mcpVms.Remove(vm);
            RefreshMcpCatalogState(_mcpVms.Select(entry => entry.Model).ToList());
        }

        private async void McpEnabledToggle_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as System.Windows.Controls.CheckBox)?.DataContext is not McpServerViewModel vm)
            {
                return;
            }

            await _mcpConfigApplicationService.UpdateMcpServerAsync(vm.Model);
            if (vm.IsEnabled)
            {
                try
                {
                    await McpManager.Instance.StartSingleAsync(vm.Model);
                    vm.RefreshStatus();
                }
                catch
                {
                    vm.StatusColor = "#CC3333";
                }
            }
            else
            {
                McpManager.Instance.StopSingle(vm.Model.Name);
            }
        }

        private async void SaveMcpBtn_Click(object sender, RoutedEventArgs e)
        {
            var config = BuildMcpConfigFromForm();
            if (string.IsNullOrWhiteSpace(config.Name) || string.IsNullOrWhiteSpace(config.Command))
            {
                McpTestResult.Text = Services.LocalizationService.S("mcp.fieldsRequired", "Name and command are required.");
                return;
            }

            if (_editingMcpVm == null)
            {
                var id = await _mcpConfigApplicationService.InsertMcpServerAsync(config);
                config.Id = id;
                var vm = new McpServerViewModel(config);
                _mcpVms.Add(vm);
                if (config.IsEnabled)
                {
                    try
                    {
                        await McpManager.Instance.StartSingleAsync(config);
                        vm.RefreshStatus();
                    }
                    catch
                    {
                        vm.StatusColor = "#CC3333";
                    }
                }
            }
            else
            {
                config.Id = _editingMcpVm.Model.Id;
                await _mcpConfigApplicationService.UpdateMcpServerAsync(config);
                _editingMcpVm.Model.Name = config.Name;
                _editingMcpVm.Model.Command = config.Command;
                _editingMcpVm.Model.Arguments = config.Arguments;
                _editingMcpVm.Model.WorkingDirectory = config.WorkingDirectory;
                _editingMcpVm.Model.EnvVars = config.EnvVars;
                if (config.IsEnabled)
                {
                    try
                    {
                        await McpManager.Instance.StartSingleAsync(config);
                        _editingMcpVm.RefreshStatus();
                    }
                    catch
                    {
                        _editingMcpVm.StatusColor = "#CC3333";
                    }
                }
            }

            RefreshMcpCatalogState(_mcpVms.Select(entry => entry.Model).ToList());
            McpEditPanel.Visibility = Visibility.Collapsed;
            _editingMcpVm = null;
        }

        private void CancelMcpBtn_Click(object sender, RoutedEventArgs e)
        {
            McpEditPanel.Visibility = Visibility.Collapsed;
            _editingMcpVm = null;
        }

        private async void TestMcpBtn_Click(object sender, RoutedEventArgs e)
        {
            McpTestResult.Text = Services.LocalizationService.S("mcp.starting", "Starting...");
            var config = BuildMcpConfigFromForm();
            try
            {
                var client = new McpClient(config);
                await client.StartAsync();
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(15));
                await client.InitializeAsync(cts.Token);
                var tools = await client.ListToolsAsync(cts.Token);
                client.Stop();
                McpTestResult.Text = string.Format(Services.LocalizationService.S("mcp.connected", "\u2713 Connected \u2014 {0} tool(s) available"), tools.Count);
            }
            catch
            {
            McpTestResult.Text = Services.LocalizationService.S("mcp.connectionFailed", "\u2717 Connection failed.");
            }
        }

        private McpServerConfig BuildMcpConfigFromForm()
        {
            var envVars = new Dictionary<string, string>();
            foreach (var line in McpEnvVarsBox.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var idx = line.IndexOf('=');
                if (idx > 0)
                {
                    envVars[line[..idx].Trim()] = line[(idx + 1)..].Trim();
                }
            }

            return new McpServerConfig
            {
                Name = McpNameBox.Text.Trim(),
                Command = McpCommandBox.Text.Trim(),
                Arguments = McpArgsBox.Text.Trim(),
                WorkingDirectory = McpWorkDirBox.Text.Trim(),
                EnvVars = envVars,
                IsEnabled = true,
            };
        }
    }
}
