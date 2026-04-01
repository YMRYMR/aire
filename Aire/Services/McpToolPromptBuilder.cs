using System.Text;

namespace Aire.Services
{
    internal static class McpToolPromptBuilder
    {
        public static string BuildSection(IReadOnlyList<Mcp.McpToolDefinition> mcpTools)
        {
            if (mcpTools.Count == 0)
                return string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine("\n\nMCP TOOLS (external connectors):");
            sb.AppendLine("These tools are provided by connected MCP servers. Call them exactly as you would built-in tools.");
            foreach (var tool in mcpTools)
                sb.AppendLine($"- {tool.Name}: {tool.Description}  [server: {tool.ServerName}]");
            return sb.ToString();
        }
    }
}
