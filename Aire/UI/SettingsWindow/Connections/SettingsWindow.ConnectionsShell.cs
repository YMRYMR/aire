using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Aire.UI.Settings.Models;

namespace Aire.UI
{
    public partial class SettingsWindow
    {
        private static readonly (string Name, string Command, string Args, string EnvHint)[] _mcpTemplates =
        {
            ("GitHub",       "npx", "-y @modelcontextprotocol/server-github",      "GITHUB_PERSONAL_ACCESS_TOKEN=<paste-token-here>"),
            ("Filesystem",   "npx", "-y @modelcontextprotocol/server-filesystem",  ""),
            ("SQLite",       "npx", "-y @modelcontextprotocol/server-sqlite",      ""),
            ("Brave Search", "npx", "-y @modelcontextprotocol/server-brave-search","BRAVE_API_KEY=<paste-api-key-here>"),
            ("Web Fetch",    "npx", "-y @modelcontextprotocol/server-fetch",       ""),
            ("Memory",       "npx", "-y @modelcontextprotocol/server-memory",      ""),
            ("Slack",        "npx", "-y @modelcontextprotocol/server-slack",       "SLACK_BOT_TOKEN=<paste-bot-token-here>\nSLACK_TEAM_ID=<paste-team-id-here>"),
            ("Gmail (OAuth)","npx", "-y @modelcontextprotocol/server-gmail",       "GMAIL_CLIENT_ID=<paste-client-id-here>\nGMAIL_CLIENT_SECRET=<paste-client-secret-here>\nGMAIL_REFRESH_TOKEN=<paste-refresh-token-here>"),
        };

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

            BuildMcpTemplatesMenu();
        }

        private void BuildMcpTemplatesMenu()
        {
            McpTemplatesMenu.Items.Clear();
            foreach (var (name, cmd, args, env) in _mcpTemplates)
            {
                var item = new System.Windows.Controls.MenuItem { Header = name };
                var captured = (name, cmd, args, env);
                item.Click += (_, _) => ApplyMcpTemplate(captured.name, captured.cmd, captured.args, captured.env);
                McpTemplatesMenu.Items.Add(item);
            }
        }

        private void ApplyMcpTemplate(string name, string cmd, string args, string env)
        {
            McpNameBox.Text = name;
            McpCommandBox.Text = cmd;
            McpArgsBox.Text = args;
            McpEnvVarsBox.Text = env;
            McpEditPanel.Visibility = Visibility.Visible;
            McpEditTitle.Text = $"Add {name}";
            _editingMcpVm = null;
        }

        private void AddGmailBtn_Click(object sender, RoutedEventArgs e)
            => OpenEmailEdit(Aire.Services.Email.EmailAccount.GmailPreset("Gmail", ""), isNew: true);

        private void AddOutlookBtn_Click(object sender, RoutedEventArgs e)
            => OpenEmailEdit(Aire.Services.Email.EmailAccount.OutlookPreset("Outlook", ""), isNew: true);

        private void AddCustomEmailBtn_Click(object sender, RoutedEventArgs e)
            => OpenEmailEdit(new Aire.Services.Email.EmailAccount(), isNew: true);
    }
}
