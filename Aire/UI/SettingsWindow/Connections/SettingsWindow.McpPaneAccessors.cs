using Aire.UI.Settings.Controls;
using Border = System.Windows.Controls.Border;
using Button = System.Windows.Controls.Button;
using ContextMenu = System.Windows.Controls.ContextMenu;
using ListView = System.Windows.Controls.ListView;
using TextBox = System.Windows.Controls.TextBox;
using TextBlock = System.Windows.Controls.TextBlock;

namespace Aire.UI
{
    public partial class SettingsWindow
    {
        private McpConnectionsPaneControl McpConnectionsPane => McpConnectionsPaneControl;
        private TextBlock McpServersTitle => McpConnectionsPane.McpServersTitle;
        private TextBlock McpServersDescription => McpConnectionsPane.McpServersDescription;
        private Button AddMcpBtn => McpConnectionsPane.AddMcpBtn;
        private Button McpTemplatesBtn => McpConnectionsPane.McpTemplatesBtn;
        private ContextMenu McpTemplatesMenu => McpConnectionsPane.McpTemplatesMenu;
        private TextBlock McpCatalogTitle => McpConnectionsPane.McpCatalogTitle;
        private ListView McpCatalogList => McpConnectionsPane.McpCatalogList;
        private ListView McpServersList => McpConnectionsPane.McpServersList;
        private TextBlock McpTipText => McpConnectionsPane.McpTipText;
        private Border McpEditPanel => McpConnectionsPane.McpEditPanel;
        private TextBlock McpEditTitle => McpConnectionsPane.McpEditTitle;
        private TextBox McpNameBox => McpConnectionsPane.McpNameBox;
        private TextBox McpCommandBox => McpConnectionsPane.McpCommandBox;
        private TextBox McpArgsBox => McpConnectionsPane.McpArgsBox;
        private TextBox McpWorkDirBox => McpConnectionsPane.McpWorkDirBox;
        private TextBox McpEnvVarsBox => McpConnectionsPane.McpEnvVarsBox;
        private Button SaveMcpBtn => McpConnectionsPane.SaveMcpBtn;
        private Button CancelMcpBtn => McpConnectionsPane.CancelMcpBtn;
        private Button TestMcpBtn => McpConnectionsPane.TestMcpBtn;
        private TextBlock McpTestResult => McpConnectionsPane.McpTestResult;
    }
}
