using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
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
            McpEditTitle.Text = "Add MCP server";
            _editingMcpVm = null;
            McpEditPanel.Visibility = Visibility.Visible;
        }

        private void EditMcpBtn_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as System.Windows.Controls.Button)?.Tag is not McpServerViewModel vm)
            {
                return;
            }

            _editingMcpVm = vm;
            McpEditTitle.Text = $"Edit {vm.Name}";
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

            if (MessageBox.Show($"Remove '{vm.Name}'?", "Confirm", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
            {
                return;
            }

            McpManager.Instance.StopSingle(vm.Model.Name);
            await _mcpConfigApplicationService.DeleteMcpServerAsync(vm.Model.Id);
            _mcpVms.Remove(vm);
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
                McpTestResult.Text = "Name and command are required.";
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
            McpTestResult.Text = "Starting...";
            var config = BuildMcpConfigFromForm();
            try
            {
                var client = new McpClient(config);
                await client.StartAsync();
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(15));
                await client.InitializeAsync(cts.Token);
                var tools = await client.ListToolsAsync(cts.Token);
                client.Stop();
                McpTestResult.Text = $"\u2713 Connected \u2014 {tools.Count} tool{(tools.Count == 1 ? "" : "s")} available";
            }
            catch (Exception ex)
            {
                McpTestResult.Text = $"\u2717 {ex.Message}";
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
