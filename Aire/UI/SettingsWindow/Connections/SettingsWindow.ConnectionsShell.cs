using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Aire.UI.Settings.Models;

namespace Aire.UI
{
    public partial class SettingsWindow
    {
        private async void TabControl_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0 && e.AddedItems[0] is System.Windows.Controls.TabItem tab && tab == TabConnections)
                await LoadConnectionsTabAsync();
        }

        private async Task LoadConnectionsTabAsync()
        {
            var emails = await _emailAccountApplicationService.GetEmailAccountsAsync();
            _emailVms.Clear();
            foreach (var account in emails) _emailVms.Add(new EmailAccountViewModel(account));
            EmailAccountsList.ItemsSource = _emailVms;

            var servers = await _mcpConfigApplicationService.GetMcpServersAsync();
            _mcpVms.Clear();
            foreach (var server in servers) _mcpVms.Add(new McpServerViewModel(server));
            McpServersList.ItemsSource = _mcpVms;
            RefreshMcpCatalogState(servers);

            BuildMcpTemplatesMenu();
        }

        private void BuildMcpTemplatesMenu()
        {
            McpTemplatesMenu.Items.Clear();
            foreach (var entry in _mcpCatalogApplicationService.GetCatalog())
            {
                var item = new System.Windows.Controls.MenuItem { Header = entry.Name };
                var capturedKey = entry.Key;
                item.Click += (_, _) => ApplyMcpTemplate(capturedKey);
                McpTemplatesMenu.Items.Add(item);
            }
        }

        private void ApplyMcpTemplate(string key)
        {
            var config = _mcpCatalogApplicationService.BuildConfig(key);
            McpNameBox.Text = config.Name;
            McpCommandBox.Text = config.Command;
            McpArgsBox.Text = config.Arguments;
            McpEnvVarsBox.Text = string.Join("\n", config.EnvVars.Select(kv => $"{kv.Key}={kv.Value}"));
            McpEditPanel.Visibility = Visibility.Visible;
            McpEditTitle.Text = $"Add {config.Name}";
            _editingMcpVm = null;
        }

        private void RefreshMcpCatalogState(IReadOnlyList<Aire.Services.Mcp.McpServerConfig> servers)
        {
            if (_mcpCatalogVms.Count == 0)
            {
                foreach (var entry in _mcpCatalogApplicationService.GetCatalog())
                    _mcpCatalogVms.Add(new McpCatalogEntryViewModel(entry));
                McpCatalogList.ItemsSource = _mcpCatalogVms;
            }

            foreach (var vm in _mcpCatalogVms)
                vm.RefreshInstalled(_mcpCatalogApplicationService, servers);
        }

        private void AddGmailBtn_Click(object sender, RoutedEventArgs e)
            => OpenEmailEdit(Aire.Services.Email.EmailAccount.GmailPreset("Gmail", ""), isNew: true);

        private void AddOutlookBtn_Click(object sender, RoutedEventArgs e)
            => OpenEmailEdit(Aire.Services.Email.EmailAccount.OutlookPreset("Outlook", ""), isNew: true);

        private void AddCustomEmailBtn_Click(object sender, RoutedEventArgs e)
            => OpenEmailEdit(new Aire.Services.Email.EmailAccount(), isNew: true);
    }
}
