namespace Aire.UI
{
    public partial class SettingsWindow
    {
        private void WireMcpPaneEvents()
        {
            McpConnectionsPane.AddMcpClicked += AddMcpBtn_Click;
            McpConnectionsPane.McpTemplatesClicked += McpTemplatesBtn_Click;
            McpConnectionsPane.CatalogMcpActionClicked += CatalogMcpActionBtn_Click;
            McpConnectionsPane.McpServersSelectionChanged += McpServersList_SelectionChanged;
            McpConnectionsPane.McpEnabledToggleClicked += McpEnabledToggle_Click;
            McpConnectionsPane.EditMcpClicked += EditMcpBtn_Click;
            McpConnectionsPane.DeleteMcpClicked += DeleteMcpBtn_Click;
            McpConnectionsPane.SaveMcpClicked += SaveMcpBtn_Click;
            McpConnectionsPane.CancelMcpClicked += CancelMcpBtn_Click;
            McpConnectionsPane.TestMcpClicked += TestMcpBtn_Click;
        }
    }
}
